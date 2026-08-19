using System;
using System.Collections.Generic;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Tracks how long the current distracting stretch has run and how often the user has
    /// been switching, and folds both into one risk estimate.
    /// </summary>
    /// <remarks>
    /// Registered as a singleton and called on a short timer (see the App-layer
    /// orchestrator), not only on window switch - "how long has this gone on" has to keep
    /// advancing while the user sits still in one distracting window, which a switch-only
    /// callback would never observe.
    /// </remarks>
    public sealed class DistractionDetector : IDistractionDetector
    {
        /// <summary>How far back "recent" switches are counted.</summary>
        private static readonly TimeSpan SwitchWindow = TimeSpan.FromHours(1);

        /// <summary>
        /// A gap of true stillness this long resets the streak. Small tolerance: a
        /// classification tick every ten seconds should not itself look like a gap.
        /// </summary>
        private static readonly TimeSpan ContinuityTolerance = TimeSpan.FromSeconds(30);

        private readonly object _gate = new();
        private readonly Queue<DateTimeOffset> _recentSwitches = new();

        private DateTimeOffset? _distractionStart;
        private DateTimeOffset? _lastObservation;
        private string? _lastApp;
        private string? _lastProductiveApp;

        public DistractionSignal? Observe(ActivityContext context, ProductivityVerdict verdict, DateTimeOffset now)
        {
            lock (_gate)
            {
                PruneSwitches(now);

                if (verdict.IsProductive)
                {
                    _distractionStart = null;
                    _lastApp = context.AppName;
                    _lastProductiveApp = context.AppName;
                    _lastObservation = now;
                    return null;
                }

                // A gap longer than tolerance, or a different app since the last tick, both
                // mean this is not a continuation of whatever stretch was being timed.
                var isContinuation = _distractionStart is not null
                    && _lastApp == context.AppName
                    && _lastObservation is not null
                    && now - _lastObservation.Value <= ContinuityTolerance;

                if (!isContinuation)
                    _distractionStart = now;

                _lastApp = context.AppName;
                _lastObservation = now;

                var continuous = now - _distractionStart!.Value;
                var switches = _recentSwitches.Count;

                return new DistractionSignal(
                    Context: context,
                    Verdict: verdict,
                    ContinuousDistractionTime: continuous,
                    RecentAppSwitches: switches,
                    GoalRelevance: null,
                    Risk: ComputeRisk(verdict, continuous, switches));
            }
        }

        public void RecordSwitch(DateTimeOffset when)
        {
            lock (_gate)
            {
                _recentSwitches.Enqueue(when);
                PruneSwitches(when);
            }
        }

        public string? LastProductiveApp
        {
            get { lock (_gate) return _lastProductiveApp; }
        }

        /// <summary>Caller must hold <see cref="_gate"/>.</summary>
        private void PruneSwitches(DateTimeOffset now)
        {
            while (_recentSwitches.Count > 0 && now - _recentSwitches.Peek() > SwitchWindow)
                _recentSwitches.Dequeue();
        }

        /// <summary>
        /// Blends three things that each say something different: how sure the classifier
        /// is, how long this has gone on, and how much thrashing has surrounded it. None of
        /// them alone is a good proxy - a confident five-second glance at Slack is not a
        /// risk, and an unconfident hour is still an hour.
        /// </summary>
        private static double ComputeRisk(ProductivityVerdict verdict, TimeSpan continuous, int recentSwitches)
        {
            var confidence = verdict.Confidence;

            // Ramps from 0 at zero minutes to 1 at fifteen minutes, then holds - risk from
            // duration alone should not keep climbing forever.
            var durationFactor = Math.Clamp(continuous.TotalMinutes / 15.0, 0, 1);

            // Anything past roughly one switch a minute over the last hour reads as
            // thrashing rather than normal task-switching.
            var thrashingFactor = Math.Clamp(recentSwitches / 60.0, 0, 1);

            return Math.Clamp(confidence * 0.5 + durationFactor * 0.35 + thrashingFactor * 0.15, 0, 1);
        }
    }
}
