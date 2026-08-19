using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Intelligence;
using FocusAssistant.Core.Reports;
using FocusAssistant.Data.Queries;
using FocusAssistant.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenXmlParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using OpenXmlText = DocumentFormat.OpenXml.Wordprocessing.Text;

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
        [ObservableProperty] private bool _isStatusVisible;
        [ObservableProperty] private bool _isStatusError;

        // Cancel-and-recreate so a second save inside the four-second window restarts the
        // clock instead of the first save's timer hiding the second save's message early.
        private CancellationTokenSource? _statusCancellation;

        // Same reasoning as TodayViewModel's gate: only effective because this view model
        // is registered Singleton now, so a revisit is the same instance asking "have I
        // already loaded this recently" rather than a fresh one with no memory of anything.
        // Longer than Today's window - a week or a month of activity does not meaningfully
        // change from one tab switch to the next the way "right now" can.
        private readonly RefreshGate _refreshGate = new(TimeSpan.FromSeconds(30));

        // Every LoadAsync (i.e. every visit to this screen, since the view model is
        // transient) starts a fresh generation. Without cancelling the previous one,
        // switching Today -> Insights -> Today -> Insights a few times in a row queues up
        // several ~15 second CPU-bound generations back to back on the model's single-slot
        // semaphore - each one individually harmless, but stacked they make the whole app
        // feel stuck for the time it takes to work through the backlog.
        private CancellationTokenSource? _observationsCancellation;
        private (DateTime From, DateTime To) _loadedRange;

        /// <summary>Average productive share per hour of the day, across the whole period.</summary>
        public ObservableCollection<HourlyFocus> HourlyPattern { get; } = [];

        /// <summary>Applications that ate the most time, ranked by minutes actually lost.</summary>
        public ObservableCollection<AppShare> DistractionSources { get; } = [];

        public InsightsViewModel(
            DayQueryService days,
            ILocalLanguageModel languageModel,
            StartupState startupState,
            ILogger<InsightsViewModel> logger)
        {
            _days = days ?? throw new ArgumentNullException(nameof(days));
            _languageModel = languageModel ?? throw new ArgumentNullException(nameof(languageModel));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        partial void OnPeriodChanged(InsightsPeriod value)
        {
            // The period just changed, so "I already loaded this recently" is no longer
            // true for what the screen is now supposed to show - the cache has to be
            // bypassed here even though it would otherwise still be within its window.
            _refreshGate.Invalidate();
            _ = LoadAsync();
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (!await _startupState.DatabaseReady)
                return;

            if (!_refreshGate.ShouldRefresh(DateTimeOffset.Now))
            {
                _logger.LogDebug("Insights: cache hit, skipping reload");
                return;
            }

            _logger.LogDebug("Insights: cache miss, reloading");
            IsLoading = true;
            try
            {
                var (from, to) = RangeFor(Period);
                _loadedRange = (from, to);
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

                _refreshGate.MarkRefreshed(DateTimeOffset.Now);
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
            _observationsCancellation?.Cancel();
            _observationsCancellation = new CancellationTokenSource();
            var ct = _observationsCancellation.Token;

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
                    MaxNewTokens: 130), ct);

                if (!ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(written))
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

        /// <summary>
        /// Turns what is already on screen into a document worth keeping, rather than a raw
        /// CSV row dump nobody re-opens. Reuses the figures already computed by
        /// <see cref="LoadAsync"/> - no extra queries, and no extra model call, since
        /// <see cref="Observations"/> already holds either the written or templated summary.
        /// </summary>
        [RelayCommand]
        private async Task DownloadReportAsync()
        {
            var dialog = new SaveFileDialog
            {
                FileName = $"focus-report-{_loadedRange.From:yyyy-MM-dd}-{_loadedRange.To.AddDays(-1):yyyy-MM-dd}.docx",
                Filter = "Word document (*.docx)|*.docx",
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                // Off the UI thread: OpenXml's part-writing does file I/O and XML
                // serialisation neither of which belongs on the thread the window paints on.
                await Task.Run(() => WriteReportDocx(dialog.FileName));
                ShowStatus("Report saved.", isError: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not generate the report");
                ShowStatus("Couldn't save the report.", isError: true);
            }
        }

        /// <summary>
        /// A brief toast rather than a permanent label: the file's location is not
        /// information worth keeping on screen once the save has happened, and a raw path
        /// sitting there forever reads as leftover debug output.
        /// </summary>
        private void ShowStatus(string message, bool isError)
        {
            _statusCancellation?.Cancel();
            _statusCancellation = new CancellationTokenSource();
            var ct = _statusCancellation.Token;

            Status = message;
            IsStatusError = isError;
            IsStatusVisible = true;

            _ = HideStatusAfterDelayAsync(ct);
        }

        private async Task HideStatusAfterDelayAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4), ct);
                IsStatusVisible = false;
            }
            catch (TaskCanceledException)
            {
                // Superseded by a newer save; the newer call owns hiding the toast now.
            }
        }

        private void WriteReportDocx(string filePath)
        {
            var (from, to) = _loadedRange;

            using var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            body.AppendChild(Heading("Focus Report", 32));
            body.AppendChild(Paragraph($"{from:MMM d} to {to.AddDays(-1):MMM d, yyyy}", italic: true));
            body.AppendChild(Paragraph(string.Empty));

            body.AppendChild(Paragraph($"Focus score: {FocusScore}/100 ({ScoreBand})"));
            body.AppendChild(Paragraph($"Focused time: {FocusedTime}"));
            body.AppendChild(Paragraph($"Distracted time: {DistractedTime}"));
            body.AppendChild(Paragraph($"Longest unbroken stretch: {LongestStretch}"));
            body.AppendChild(Paragraph($"Days tracked: {DaysWithActivity}"));

            body.AppendChild(Heading("Observations", 26));
            foreach (var line in Observations.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim().TrimStart('-', ' ');
                if (trimmed.Length > 0)
                    body.AppendChild(Bullet(trimmed));
            }

            if (DistractionSources.Count > 0)
            {
                body.AppendChild(Heading("Biggest distractions", 26));
                foreach (var app in DistractionSources)
                    body.AppendChild(Bullet($"{app.Name} - {app.Time}"));
            }

            var activeHours = HourlyPattern.Where(h => h.TotalMinutes > 0)
                .OrderByDescending(h => h.ProductiveMinutes).Take(5).ToList();
            if (activeHours.Count > 0)
            {
                body.AppendChild(Heading("When you were sharpest", 26));
                foreach (var hour in activeHours)
                    body.AppendChild(Bullet($"{hour.Label}: {hour.ProductiveMinutes:F0} min focused"));
            }

            body.AppendChild(Paragraph(string.Empty));
            body.AppendChild(Paragraph(
                $"Generated {DateTime.Now:MMM d, yyyy h:mm tt} by Focus Assistant, entirely on this device.",
                italic: true));

            mainPart.Document.Save();
        }

        private static OpenXmlParagraph Heading(string text, int fontSizeHalfPoints) =>
            new(new Run(
                new RunProperties(new Bold(), new FontSize { Val = fontSizeHalfPoints.ToString() }),
                new OpenXmlText(text) { Space = SpaceProcessingModeValues.Preserve }))
            {
                ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines { Before = "300", After = "150" }),
            };

        private static OpenXmlParagraph Paragraph(string text, bool italic = false)
        {
            var runProperties = new RunProperties();
            if (italic)
                runProperties.Append(new Italic());

            return new OpenXmlParagraph(new Run(runProperties, new OpenXmlText(text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        private static OpenXmlParagraph Bullet(string text) =>
            new(new Run(new OpenXmlText($"• {text}") { Space = SpaceProcessingModeValues.Preserve }));

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
