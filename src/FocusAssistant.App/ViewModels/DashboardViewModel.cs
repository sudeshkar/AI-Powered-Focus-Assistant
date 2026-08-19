using CommunityToolkit.Mvvm.ComponentModel;
using FocusAssistant.Hosting;
using FocusAssistant.Core.Reports;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.ViewModels
{
    /// <summary>Backs the Dashboard view with today's headline figures.</summary>
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IReportGenerator _reportGenerator;
        private readonly StartupState _startupState;

        [ObservableProperty]
        private string productivityRate = "0%";

        [ObservableProperty]
        private string totalActivities = "0";

        [ObservableProperty]
        private string recentInterventions = "0";

        public ObservableCollection<KeyValuePair<string, int>> TopApps { get; } = new();

        public int TopAppsCount => TopApps.Count;

        public DashboardViewModel(IReportGenerator reportGenerator, StartupState startupState)
        {
            _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
        }

        public async Task LoadAsync()
        {
            // Migrations run on a background thread so the window can paint at once, so
            // every read has to wait for the schema to exist. Without this the first run
            // logs "no such table" and silently shows an empty screen.
            if (!await _startupState.DatabaseReady)
                return;

            var report = await _reportGenerator.GetTodayReportAsync();

            ProductivityRate = $"{report.ProductivityRate:F0}%";
            TotalActivities = report.TotalActivities.ToString();
            RecentInterventions = report.RecentInterventions.ToString();

            TopApps.Clear();
            foreach (var app in report.TopApps.OrderByDescending(a => a.Value))
                TopApps.Add(app);

            OnPropertyChanged(nameof(TopAppsCount));
        }
    }
}
