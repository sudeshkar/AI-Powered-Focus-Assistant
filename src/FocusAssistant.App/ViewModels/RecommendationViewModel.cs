using CommunityToolkit.Mvvm.ComponentModel;
using FocusAssistant.Hosting;
using CommunityToolkit.Mvvm.Input;
using FocusAssistant.Core.Reports;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.ViewModels
{
    /// <summary>
    /// Backs the Recommendations view. Today this shows today's stats-driven
    /// summary; Phase 6 replaces the placeholder text with a written readout from
    /// the local model (or, until that model is downloaded, a stronger template).
    /// </summary>
    public partial class RecommendationViewModel : ObservableObject
    {
        private readonly IReportGenerator _reportGenerator;
        private readonly StartupState _startupState;

        [ObservableProperty]
        private string headline = "No data yet - start a session to see insights here.";

        [ObservableProperty]
        private string topAppsSummary = "N/A";

        [ObservableProperty]
        private string insightBody =
            "Daily written insights are generated on-device once enough activity has " +
            "been recorded. This card will summarise what helped and what broke your " +
            "focus once that lands.";

        public RecommendationViewModel(IReportGenerator reportGenerator, StartupState startupState)
        {
            _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));

            // Not started here: see AnalyticsViewModel. The view calls LoadAsync on Loaded.
        }

        [RelayCommand]
        private Task RefreshAsync() => LoadAsync();

        public async Task LoadAsync()
        {
            try
            {
                if (!await _startupState.DatabaseReady)
                    return;

                var report = await _reportGenerator.GetTodayReportAsync();

                Headline = report.TotalActivities == 0
                    ? "No activity recorded today yet."
                    : $"{report.ProductivityRate:F0}% productive today across {report.TotalActivities} tracked activities.";

                TopAppsSummary = report.TopApps.Count == 0
                    ? "No apps tracked yet."
                    : string.Join(", ", report.TopApps.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key));
            }
            catch (Exception ex)
            {
                Headline = $"Could not load today's data: {ex.Message}";
            }
        }
    }
}
