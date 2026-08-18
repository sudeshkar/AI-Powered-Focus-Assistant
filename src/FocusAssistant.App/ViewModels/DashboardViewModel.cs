using CommunityToolkit.Mvvm.ComponentModel;
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

        [ObservableProperty]
        private string productivityRate = "0%";

        [ObservableProperty]
        private string totalActivities = "0";

        [ObservableProperty]
        private string recentInterventions = "0";

        public ObservableCollection<KeyValuePair<string, int>> TopApps { get; } = new();

        public int TopAppsCount => TopApps.Count;

        public DashboardViewModel(IReportGenerator reportGenerator)
        {
            _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        }

        public async Task LoadAsync()
        {
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
