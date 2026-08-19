using FocusAssistant.Core.Focus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FocusAssistant.Core.Intervention
{
    /// <summary>
    /// Decides whether a distraction signal is worth interrupting someone over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default answer is no, and every rule here exists to keep it no more often than
    /// not: never nudge on a guess, never in the first ninety seconds, never more than a
    /// handful of times a day, never during a call, and less and less for an app the user
    /// has already dismissed twice. A policy that speaks often is a policy people mute or
    /// uninstall, and undoing that trust is much harder than earning it slowly.
    /// </para>
    /// <para>
    /// Registered as a singleton; all of its state - cadence, per-app thresholds, dismissal
    /// counts - lives in memory for the process lifetime and resets with it. That is a
    /// deliberate simplification: cadence limits that reset at restart cost nothing, and
    /// persisting them would only matter to somebody restarting the app specifically to
    /// dodge a cooldown.
    /// </para>
    /// </remarks>
    public sealed class InterventionPolicy : IInterventionPolicy
    {
        private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan MinimumGapBetweenNudges = TimeSpan.FromMinutes(12);
        private const int MaxPerHour = 4;
        private const int MaxPerDay = 12;

        /// <summary>Escalate from Ambient to Toast only once a stretch has run this long.</summary>
        private static readonly TimeSpan ToastThreshold = TimeSpan.FromMinutes(4);

        /// <summary>
        /// Applications whose foreground presence means a call is probably underway. Kept
        /// narrow and name-based: false positives here (staying silent when it was actually
        /// fine to speak) cost nothing, while a nudge popping up over a screen share costs
        /// the user's trust in the whole feature.
        /// </summary>
        private static readonly HashSet<string> CallApps = new(StringComparer.OrdinalIgnoreCase)
        {
            "Teams", "ms-teams", "zoom", "Zoom", "Slack", "discord", "Discord", "Skype",
        };

        private readonly object _gate = new();
        private readonly Queue<DateTimeOffset> _recentNudges = new();
        private readonly ConcurrentDictionary<string, AppState> _perApp =
            new(StringComparer.OrdinalIgnoreCase);

        public InterventionSuggestion? Decide(DistractionSignal signal, DateTimeOffset now)
        {
            // A guess has not earned the right to interrupt anyone - "I am not sure" and
            // "please stop what you are doing" cannot both be true.
            if (signal.Verdict.Source == Focus.ClassificationSource.Default)
                return null;

            if (signal.ContinuousDistractionTime < GracePeriod)
                return null;

            if (IsLikelyOnACall(signal.Context.AppName))
                return null;

            var appState = _perApp.GetOrAdd(signal.Context.AppName, _ => new AppState());
            if (appState.IsOverridden)
                return null;

            lock (_gate)
            {
                PruneNudges(now);

                if (_recentNudges.Count >= MaxPerDay)
                    return null;

                if (CountSince(now.AddHours(-1)) >= MaxPerHour)
                    return null;

                if (appState.LastShown is { } last && now - last < AppCooldown(appState))
                    return null;

                var tier = signal.ContinuousDistractionTime >= ToastThreshold
                    ? InterventionTier.Toast
                    : InterventionTier.Ambient;

                // Overlay is opt-in only, and only after the user has already ignored
                // several toasts in this app during this session - it is never the first
                // thing shown for anything.
                if (tier == InterventionTier.Toast && appState.ConsecutiveIgnored >= 3 && appState.OverlayAllowed)
                    tier = InterventionTier.Overlay;

                _recentNudges.Enqueue(now);
                appState.LastShown = now;

                return new InterventionSuggestion
                {
                    Message = BuildMessage(signal),
                    Tier = tier,
                    DistractionRisk = signal.Risk,
                    AppName = signal.Context.AppName,
                    Rationale = signal.Verdict.Rationale,
                };
            }
        }

        public void RecordResponse(InterventionSuggestion suggestion, InterventionResponse response, DateTimeOffset when)
        {
            var appState = _perApp.GetOrAdd(suggestion.AppName, _ => new AppState());

            switch (response)
            {
                case InterventionResponse.DismissedPolitely:
                    appState.DismissCount++;
                    appState.ConsecutiveIgnored = 0;

                    // Two dismissals on the same app is the user telling us, twice, that
                    // this classification is wrong for them - at that point continuing to
                    // ask is not persistence, it is not listening.
                    if (appState.DismissCount >= 2)
                        appState.IsOverridden = true;
                    break;

                case InterventionResponse.Ignored:
                    appState.ConsecutiveIgnored++;
                    break;

                case InterventionResponse.ActedImmediately:
                case InterventionResponse.ActedLater:
                    appState.ConsecutiveIgnored = 0;
                    break;
            }
        }

        /// <summary>Caller must hold <see cref="_gate"/>.</summary>
        private void PruneNudges(DateTimeOffset now)
        {
            var dayAgo = now.AddDays(-1);
            while (_recentNudges.Count > 0 && _recentNudges.Peek() < dayAgo)
                _recentNudges.Dequeue();
        }

        /// <summary>Caller must hold <see cref="_gate"/>.</summary>
        private int CountSince(DateTimeOffset since)
        {
            var count = 0;
            foreach (var nudge in _recentNudges)
            {
                if (nudge >= since)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// The per-app cooldown, which grows the more this app has been dismissed - de-
        /// escalation without a hard cutoff, so a genuinely useful nudge is not permanently
        /// silenced by one impatient dismissal earlier in the day.
        /// </summary>
        private static TimeSpan AppCooldown(AppState state) =>
            MinimumGapBetweenNudges * (1 + state.DismissCount);

        private static bool IsLikelyOnACall(string appName) => CallApps.Contains(appName);

        /// <summary>
        /// Descriptive, not moralising. Names the app and how long, states the goal when
        /// there is one, and asks rather than accuses.
        /// </summary>
        private static string BuildMessage(DistractionSignal signal)
        {
            var minutes = (int)Math.Ceiling(signal.ContinuousDistractionTime.TotalMinutes);
            var duration = minutes <= 1 ? "a couple of minutes" : $"{minutes} minutes";

            return string.IsNullOrWhiteSpace(signal.Context.SessionGoal)
                ? $"{duration} in {signal.Context.AppName}. Still on track?"
                : $"{duration} in {signal.Context.AppName} since you started \"{signal.Context.SessionGoal}\".";
        }

        private sealed class AppState
        {
            public DateTimeOffset? LastShown;
            public int DismissCount;
            public int ConsecutiveIgnored;
            public bool IsOverridden;

            /// <summary>
            /// Escalation to Overlay must be switched on somewhere the user can see and
            /// undo it (Settings, in a later phase); until that exists it stays off, so the
            /// ladder in practice never exceeds Toast.
            /// </summary>
            public bool OverlayAllowed;
        }
    }
}
