using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.ViewModels
{
    /// <summary>
    /// Backs the Dashboard view with today's headline figures.
    /// </summary>
    public class DashboardViewModel : ObservableObject
    {
        private readonly Services.Session.Interfaces.IReportGenerator _reportGenerator;

        private string _productivityRate = "0%";
        private string _totalActivities = "0";
        private string _recentInterventions = "0";

        public DashboardViewModel(Services.Session.Interfaces.IReportGenerator reportGenerator)
        {
            _reportGenerator = reportGenerator;
        }

        public string ProductivityRate { get => _productivityRate; set => SetProperty(ref _productivityRate, value); }
        public string TotalActivities { get => _totalActivities; set => SetProperty(ref _totalActivities, value); }
        public string RecentInterventions { get => _recentInterventions; set => SetProperty(ref _recentInterventions, value); }

        public ObservableCollection<KeyValuePair<string, int>> TopApps { get; } = new();

        public int TopAppsCount => TopApps.Count;

        /// <summary>
        /// Loads today's figures. The previous version had this method body
        /// commented out, so the dashboard always displayed zeros.
        /// </summary>
        public async Task LoadAsync()
        {
            var report = await _reportGenerator.GetReportFlask();

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
