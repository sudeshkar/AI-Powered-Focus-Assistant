using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Intelligence;
using FocusAssistant.Core.Reports;
using FocusAssistant.Data.Queries;
using FocusAssistant.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.ViewModels
{
    /// <summary>
    /// Backs the Insights screen: what patterns show up over a week or a month, as opposed
    /// to Today's single-day view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the separate Analytics and Recommendations screens. Once Today carried a
    /// real score, a real timeline, and a real written summary, the two of them had nothing
    /// left to say that Today did not already say better for a single day - Analytics was
    /// stock WPF controls reading a "Session Statistics" GroupBox, and Recommendations
    /// showed the same headline/top-apps/insight shape Today now covers properly. What
    /// neither offered was a view across more than one day, which is the actual gap.
    /// </para>
    /// <para>
    /// "Nudges shown / acted on" belongs on this screen and is not here yet: it needs
    /// InterventionOutcome, which Phase 4 has not built. Adding a stat with no data behind
    /// it would be worse than leaving it out.
    /// </para>
    /// </remarks>
    public sealed partial class InsightsViewModel : ObservableObject
    {
        private readonly DayQueryService _days;
        private readonly AnalyticsServiceSQL _analytics;
        private readonly ILocalLanguageModel _languageModel;
        private readonly StartupState _startupState;
        private readonly ILogger<InsightsViewModel> _logger;

        [ObservableProperty] private InsightsPeriod _period = InsightsPeriod.Week;

        [ObservableProperty] private int _focusScore;
        [ObservableProperty] private string _scoreBand = "No data yet";
        [ObservableProperty] private bool _hasData;
        [ObservableProperty] private bool _isLoading;

        [ObservableProperty] private string _focusedTime = "0m";
        [ObservableProperty] private string _distractedTime = "0m";
        [ObservableProperty] private string _longestStretch = "0m";
        [ObservableProperty] private int _daysWithActivity;

        [ObservableProperty] private string _observations = string.Empty;
        [ObservableProperty] private bool _isWritingObservations;

        [ObservableProperty] private string _status = string.Empty;

        /// <summary>Average productive share per hour of the day, across the whole period.</summary>
        public ObservableCollection<HourlyFocus> HourlyPattern { get; } = [];

        /// <summary>Applications that ate the most time, ranked by minutes actually lost.</summary>
        public ObservableCollection<AppShare> DistractionSources { get; } = [];

        public InsightsViewModel(
            DayQueryService days,
            AnalyticsServiceSQL analytics,
            ILocalLanguageModel languageModel,
            StartupState startupState,
            ILogger<InsightsViewModel> logger)
        {
            _days = days ?? throw new ArgumentNullException(nameof(days));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _languageModel = languageModel ?? throw new ArgumentNullException(nameof(languageModel));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        partial void OnPeriodChanged(InsightsPeriod value) => _ = LoadAsync();

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (!await _startupState.DatabaseReady)
                return;

            IsLoading = true;
            try
            {
                var (from, to) = RangeFor(Period);
                var usages = await _days.GetUsagesAsync(from, to);

                var score = FocusScorer.Score(usages, TimeSpan.Zero);

                FocusScore = score.Value;
                ScoreBand = score.Band;
                HasData = score.HasData;
                FocusedTime = Format(score.ProductiveTime);
                DistractedTime = Format(score.DistractedTime);
                LongestStretch = Format(score.LongestStretch);
                DaysWithActivity = usages.Select(u => u.StartTime.Date).Distinct().Count();

                HourlyPattern.Clear();
                foreach (var hour in DayTimeline.ByHour(usages, from))
                    HourlyPattern.Add(hour);

                var total = score.ProductiveTime + score.DistractedTime;
                DistractionSources.Clear();
                foreach (var app in usages
                             .Where(u => !u.IsProductive)
                             .GroupBy(u => u.AppName)
                             .Select(g => new
                             {
                                 Name = g.Key,
                                 Time = TimeSpan.FromTicks(g.Sum(u => u.Duration.Ticks)),
                             })
                             .OrderByDescending(a => a.Time)
                             .Take(6))
                {
                    DistractionSources.Add(new AppShare(
                        app.Name,
                        Format(app.Time),
                        total > TimeSpan.Zero ? app.Time.TotalMinutes / total.TotalMinutes : 0,
                        IsProductive: false));
                }

                if (score.HasData)
                    _ = WriteObservationsAsync(score, from, to);
                else
                    Observations = "Nothing tracked in this period yet.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not load insights");
                Status = $"Could not load insights: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Asks the local model for a handful of observations, grounded strictly in the
        /// numbers already computed.
        /// </summary>
        /// <remarks>
        /// The prompt is deliberately closed-book: it is handed the finished aggregates and
        /// told to describe them, never asked an open question about the period. A model
        /// asked to speculate about someone's week will do so fluently and wrongly; one
        /// asked to summarise five given numbers has much less room to invent a sixth.
        /// </remarks>
        private async Task WriteObservationsAsync(FocusScore score, DateTime from, DateTime to)
        {
            Observations = BuildTemplateObservations(score);

            if (_languageModel.Availability is ModelAvailability.Disabled)
                return;

            try
            {
                IsWritingObservations = true;

                var topDistraction = DistractionSources.FirstOrDefault();
                var peakHour = HourlyPattern.OrderByDescending(h => h.ProductiveMinutes).FirstOrDefault();

                var written = await _languageModel.GenerateAsync(new LlmRequest(
                    System: "You write 3 short observations as a bulleted list, one per line starting " +
                            "with a dash, about someone's focus over a period of days. Use only the " +
                            "figures given and invent nothing beyond them - copy any time given exactly " +
                            "as written rather than reformatting it. Plain and factual, never scolding. " +
                            "Each line under 20 words.",
                    User: $"Period: {from:MMM d} to {to.AddDays(-1):MMM d}. Focus score {score.Value}/100. " +
                          $"Focused {Format(score.ProductiveTime)}, distracted {Format(score.DistractedTime)} " +
                          $"across {DaysWithActivity} tracked days. Longest unbroken stretch " +
                          $"{Format(score.LongestStretch)}. " +
                          (topDistraction is not null ? $"Biggest distraction: {topDistraction.Name} ({topDistraction.Time}). " : "") +
                          // The full 12-hour clock time, not HourlyFocus.Label's compact "11a" form:
                          // the model was echoing that abbreviation back malformed as "11amam".
                          (peakHour is not null && peakHour.TotalMinutes > 0
                              ? $"Most productive hour of the day: {FormatHour(peakHour.Hour)}."
                              : ""),
                    MaxNewTokens: 130));

                if (!string.IsNullOrWhiteSpace(written))
                    Observations = written;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Written observations unavailable; keeping the templated summary");
            }
            finally
            {
                IsWritingObservations = false;
            }
        }

        private string BuildTemplateObservations(FocusScore score)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                $"- Focused {Format(score.ProductiveTime)} across {DaysWithActivity} tracked day(s), averaging a score of {score.Value}/100.",
            };

            if (DistractionSources.FirstOrDefault() is { } top)
                lines.Add($"- {top.Name} accounted for the most distracted time, at {top.Time}.");

            var peak = HourlyPattern.Where(h => h.TotalMinutes > 0).OrderByDescending(h => h.ProductiveMinutes).FirstOrDefault();
            if (peak is not null)
                lines.Add($"- Most productive minutes landed around {peak.Label}.");

            return string.Join("\n", lines);
        }

        [RelayCommand]
        private async Task DownloadReportAsync()
        {
            try
            {
                var csv = await _analytics.GenerateCsvReportAsync(DateTime.Today);

                var dialog = new SaveFileDialog
                {
                    FileName = $"focus-report-{DateTime.Today:yyyy-MM-dd}.csv",
                    Filter = "CSV files (*.csv)|*.csv",
                };

                if (dialog.ShowDialog() == true)
                {
                    await File.WriteAllTextAsync(dialog.FileName, csv);
                    Status = $"Saved to {dialog.FileName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not export the report");
                Status = $"Export failed: {ex.Message}";
            }
        }

        private static (DateTime from, DateTime to) RangeFor(InsightsPeriod period)
        {
            var today = DateTime.Today;
            return period switch
            {
                InsightsPeriod.Today => (today, today.AddDays(1)),
                InsightsPeriod.Week => (today.AddDays(-6), today.AddDays(1)),
                InsightsPeriod.Month => (today.AddDays(-29), today.AddDays(1)),
                _ => (today.AddDays(-6), today.AddDays(1)),
            };
        }

        private static string Format(TimeSpan span) =>
            span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{span.Minutes}m";

        private static string FormatHour(int hour24) =>
            DateTime.Today.AddHours(hour24).ToString("h tt", System.Globalization.CultureInfo.InvariantCulture);
    }

    public enum InsightsPeriod
    {
        Today,
        Week,
        Month,
    }
}
