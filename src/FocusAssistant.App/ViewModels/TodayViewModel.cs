using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Intelligence;
using FocusAssistant.Core.Reports;
using FocusAssistant.Data.Queries;
using FocusAssistant.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.ViewModels
{
    /// <summary>
    /// Backs the Today screen, which answers one question: how is today going?
    /// </summary>
    /// <remarks>
    /// The screen this replaces showed three tiles whose numbers were, on inspection,
    /// meaningless. "Interventions today" read UserSession.DistractionEvents, which nothing
    /// in the solution ever assigned, so it was permanently zero. "Activities tracked"
    /// summed the lengths of top-app lists, capped at five per session, so it counted
    /// list entries rather than activity. "Top apps" ranked applications by how many
    /// sessions mentioned them rather than by time spent, while the Analytics screen
    /// ranked the same concept correctly by duration - two screens, two answers.
    /// <para>
    /// Everything here is computed from the day's actual rows through
    /// <see cref="FocusScorer"/>, so every screen now agrees.
    /// </para>
    /// </remarks>
    public sealed partial class TodayViewModel : ObservableObject
    {
        private readonly DayQueryService _days;
        private readonly ILocalLanguageModel _languageModel;
        private readonly StartupState _startupState;
        private readonly ILogger<TodayViewModel> _logger;

        private CancellationTokenSource? _insightCancellation;

        // Keeps a revisit within the window instant: no database query, no timeline
        // recompute, no fresh language-model generation. Only meaningful because this
        // view model is registered Singleton - a Transient one would lose this field, and
        // the cache, on every single navigation.
        private readonly RefreshGate _refreshGate = new(TimeSpan.FromSeconds(20));

        [ObservableProperty] private int _focusScore;
        [ObservableProperty] private string _scoreBand = "No data yet";
        [ObservableProperty] private string _headline = "Nothing tracked yet today.";

        [ObservableProperty] private string _focusedTime = "0m";
        [ObservableProperty] private string _distractedTime = "0m";
        [ObservableProperty] private string _longestStretch = "0m";
        [ObservableProperty] private string _breakTime = "0m";
        [ObservableProperty] private int _appSwitches;
        [ObservableProperty] private int _streakDays;

        /// <summary>Why the score is what it is, so the number is arguable rather than opaque.</summary>
        [ObservableProperty] private string _scoreExplanation = string.Empty;

        [ObservableProperty] private bool _hasData;
        [ObservableProperty] private bool _isLoading;

        [ObservableProperty] private string _insight =
            "Once there is enough activity, a written summary of your day appears here.";
        [ObservableProperty] private bool _isWritingInsight;
        [ObservableProperty] private bool _canEnableInsights;

        /// <summary>Runs of the day coloured by what was happening, drawn as one strip.</summary>
        public ObservableCollection<TimelineSegment> Timeline { get; } = [];

        /// <summary>Applications ranked by time spent - not by how many sessions mentioned them.</summary>
        public ObservableCollection<AppShare> TopApps { get; } = [];

        public TodayViewModel(
            DayQueryService days,
            ILocalLanguageModel languageModel,
            StartupState startupState,
            ILogger<TodayViewModel> logger)
        {
            _days = days ?? throw new ArgumentNullException(nameof(days));
            _languageModel = languageModel ?? throw new ArgumentNullException(nameof(languageModel));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (!await _startupState.DatabaseReady)
                return;

            if (!_refreshGate.ShouldRefresh(DateTimeOffset.Now))
            {
                _logger.LogDebug("Today: cache hit, skipping reload");
                return;
            }

            _logger.LogDebug("Today: cache miss, reloading");
            IsLoading = true;
            try
            {
                var today = DateTime.Today;
                var usages = await _days.GetUsagesAsync(today);
                var breaks = await _days.GetBreakTimeAsync(today);

                var score = FocusScorer.Score(usages, breaks);

                FocusScore = score.Value;
                ScoreBand = score.Band;
                HasData = score.HasData;

                FocusedTime = Format(score.ProductiveTime);
                DistractedTime = Format(score.DistractedTime);
                LongestStretch = Format(score.LongestStretch);
                BreakTime = Format(score.BreakTime);
                AppSwitches = score.AppSwitches;
                StreakDays = await _days.GetStreakDaysAsync(today);

                ScoreExplanation = BuildExplanation(score);
                Headline = BuildHeadline(score);

                Timeline.Clear();
                foreach (var segment in DayTimeline.Build(usages, today))
                    Timeline.Add(segment);

                var total = score.ProductiveTime + score.DistractedTime;
                TopApps.Clear();
                foreach (var app in usages
                             .GroupBy(u => u.AppName)
                             .Select(g => new
                             {
                                 Name = g.Key,
                                 Time = TimeSpan.FromTicks(g.Sum(u => u.Duration.Ticks)),
                                 Productive = g.Count(u => u.IsProductive) >= g.Count() / 2.0,
                             })
                             .OrderByDescending(a => a.Time)
                             .Take(6))
                {
                    TopApps.Add(new AppShare(
                        app.Name,
                        Format(app.Time),
                        total > TimeSpan.Zero ? app.Time.TotalMinutes / total.TotalMinutes : 0,
                        app.Productive));
                }

                CanEnableInsights = _languageModel.Availability == ModelAvailability.NotDownloaded
                                    || _languageModel.Availability == ModelAvailability.Disabled;

                // Started, not awaited: writing takes about fifteen seconds on CPU, and the
                // screen has already got everything else it needs to be useful.
                if (score.HasData)
                    _ = WriteInsightAsync(score);

                // Marked only on success: a failed load should not be treated as "fresh" -
                // that would leave the screen showing an error for the rest of the window
                // with no way to retry by just switching tabs and back.
                _refreshGate.MarkRefreshed(DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not load today's summary");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Asks the local model to describe the day, if it is installed.
        /// </summary>
        /// <remarks>
        /// The prompt hands over the finished numbers and forbids inventing any. A small
        /// model asked an open question about someone's day will happily make one up, and a
        /// confident fabrication about your own afternoon is worse than no paragraph at all.
        /// </remarks>
        private async Task WriteInsightAsync(FocusScore score)
        {
            _insightCancellation?.Cancel();
            _insightCancellation = new CancellationTokenSource();
            var ct = _insightCancellation.Token;

            var template = BuildTemplateInsight(score);
            Insight = template;

            if (_languageModel.Availability == ModelAvailability.Disabled)
                return;

            try
            {
                IsWritingInsight = true;

                var written = await _languageModel.GenerateAsync(new LlmRequest(
                    System: "You write one short paragraph about someone's focus for the day, for " +
                            "the person themselves. Use only the figures given and invent nothing. " +
                            "Be plain and encouraging, never scolding. At most 55 words, and finish " +
                            "your last sentence.",
                    User: $"Focus score {score.Value} out of 100. Focused {Format(score.ProductiveTime)}, " +
                          $"distracted {Format(score.DistractedTime)}. Longest unbroken stretch " +
                          $"{Format(score.LongestStretch)}. {score.AppSwitches} application switches. " +
                          $"Top applications: {string.Join(", ", TopApps.Take(3).Select(a => $"{a.Name} {a.Time}"))}.",
                    MaxNewTokens: 110), ct);

                if (!ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(written))
                    Insight = written;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Written insight unavailable; keeping the templated summary");
            }
            finally
            {
                IsWritingInsight = false;
            }
        }

        /// <summary>
        /// The summary shown when there is no language model - which is most installs.
        /// </summary>
        /// <remarks>
        /// Written to be worth reading on its own rather than as an apology for a missing
        /// download. If this reads like a placeholder, the app is broken for everyone who
        /// never fetches the 2.6GB.
        /// </remarks>
        private string BuildTemplateInsight(FocusScore score)
        {
            var parts = new List<string>
            {
                $"You focused for {Format(score.ProductiveTime)} today and lost {Format(score.DistractedTime)} to distraction.",
            };

            if (score.LongestStretch >= TimeSpan.FromMinutes(25))
                parts.Add($"Your longest unbroken stretch was {Format(score.LongestStretch)}, which is real deep work.");
            else if (score.LongestStretch > TimeSpan.Zero)
                parts.Add($"Your longest unbroken stretch was only {Format(score.LongestStretch)} - the day was chopped up.");

            if (score.FragmentationPenalty > 5)
                parts.Add($"{score.AppSwitches} application switches is a lot of context changing.");

            if (TopApps.FirstOrDefault() is { } top)
                parts.Add($"Most of it was in {top.Name}.");

            return string.Join(" ", parts);
        }

        private static string BuildHeadline(FocusScore score)
        {
            if (!score.HasData)
                return "Nothing tracked yet today.";

            return score.Value switch
            {
                >= 80 => "A strong day so far.",
                >= 60 => "Going well, with some interruptions.",
                >= 40 => "A mixed day - plenty of switching.",
                _ => "Today has been fragmented so far.",
            };
        }

        private static string BuildExplanation(FocusScore score)
        {
            if (!score.HasData)
                return string.Empty;

            var parts = new List<string>
            {
                $"{Format(score.ProductiveTime)} focused of {Format(score.ProductiveTime + score.DistractedTime)} at the machine",
            };

            if (score.FragmentationPenalty >= 1)
                parts.Add($"-{score.FragmentationPenalty:F0} for switching");

            if (score.StreakBonus >= 1)
                parts.Add($"+{score.StreakBonus:F0} for a {Format(score.LongestStretch)} stretch");

            return string.Join(" · ", parts);
        }

        private static string Format(TimeSpan span) =>
            span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{span.Minutes}m";
    }

    /// <summary>One application's share of the day, ranked by time actually spent in it.</summary>
    public sealed record AppShare(string Name, string Time, double Share, bool IsProductive);
}
