using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusAssistant.Core.Models;
using FocusAssistant.Data.Queries;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FocusAssistant.ViewModels
{
    /// <summary>Backs the Analytics view: daily totals from the local database.</summary>
    public class AnalyticsViewModel : ObservableObject
    {
        private readonly AnalyticsServiceSQL _analytics;

        private SessionStatistics _statistics = new();
        private List<AppUsageSummary> _topApps = new();
        private DateTime _selectedDate = DateTime.Today;
        private string _status = string.Empty;

        public AnalyticsViewModel(AnalyticsServiceSQL analytics)
        {
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            DownloadReportCommand = new AsyncRelayCommand(DownloadReportAsync);

            _ = LoadDataAsync();
        }

        public SessionStatistics Statistics { get => _statistics; set => SetProperty(ref _statistics, value); }
        public List<AppUsageSummary> TopApps { get => _topApps; set => SetProperty(ref _topApps, value); }
        public string Status { get => _status; set => SetProperty(ref _status, value); }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                    _ = LoadDataAsync();
            }
        }

        public ICommand LoadDataCommand { get; }
        public ICommand DownloadReportCommand { get; }

        private async Task LoadDataAsync()
        {
            try
            {
                Statistics = await _analytics.GetDailyStatisticsAsync(SelectedDate);
                TopApps = await _analytics.GetTopAppsAsync(SelectedDate);
                Status = Statistics.TotalSessions == 0
                    ? $"No sessions recorded on {SelectedDate:yyyy-MM-dd}"
                    : string.Empty;
            }
            catch (Exception ex)
            {
                // Surfaced as inline status rather than a modal: this also runs from
                // the constructor, where a dialog would block the view from loading.
                Status = $"Could not load data: {ex.Message}";
                Console.WriteLine($"Analytics load failed: {ex}");
            }
        }

        private async Task DownloadReportAsync()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"FocusAssistant_Report_{SelectedDate:yyyy-MM-dd}.csv",
                };

                if (dialog.ShowDialog() != true)
                    return;

                var csv = await _analytics.GenerateCsvReportAsync(SelectedDate);
                await File.WriteAllTextAsync(dialog.FileName, csv);
                Status = $"Report saved to {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save the report.\n\n{ex.Message}",
                    "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
