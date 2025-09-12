using FocusAssistant.Models;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.SQL_analytics;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FocusAssistant.ViewModels
{
    public class AnalyticsViewModel : INotifyPropertyChanged
    {
        private readonly AnalyticsServiceSQL _analyticsServiceSQL; 
        private SessionStatistics _statistics; private List<(string AppName, TimeSpan Duration)> _topApps; 
        private DateTime _selectedDate = DateTime.Today;
        public AnalyticsViewModel(AnalyticsServiceSQL analyticsService)
        {
            _analyticsServiceSQL = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            LoadDataCommand = new AsyncRelayCommand(async () => await LoadDataAsync());
            DownloadReportCommand = new AsyncRelayCommand(async () => await DownloadReportAsync());

            // Initial load
            _ = LoadDataAsync();
        }

        public SessionStatistics Statistics
        {
            get => _statistics;
            set
            {
                _statistics = value;
                OnPropertyChanged();
            }
        }

        public List<(string AppName, TimeSpan Duration)> TopApps
        {
            get => _topApps;
            set
            {
                _topApps = value;
                OnPropertyChanged();
            }
        }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                _selectedDate = value;
                OnPropertyChanged();
                _ = LoadDataAsync(); // Use fire-and-forget for async
            }
        }

        public ICommand LoadDataCommand { get; }
        public ICommand DownloadReportCommand { get; }

        private async Task LoadDataAsync()
        {
            try
            {
                Statistics = await _analyticsServiceSQL.GetDailyStatisticsAsync(SelectedDate);
                TopApps = await _analyticsServiceSQL.GetTopAppsAsync(SelectedDate);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DownloadReportAsync()
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"FocusAssistant_Report_{SelectedDate:yyyy-MM-dd}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var csvContent = await _analyticsServiceSQL.GenerateCsvReportAsync(SelectedDate);
                    await File.WriteAllTextAsync(saveFileDialog.FileName, csvContent);

                    MessageBox.Show("Report saved successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating report: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}




