using FocusAssistant.Models.Response_Models;
using FocusAssistant.ViewModels;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    public  partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _viewModel;

        // Constructor
        public DashboardView()
        {
            InitializeComponent();

            // Initialize the ViewModel and set it as DataContext
            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;

            Loaded += async (s, e) => await LoadDashboardDataAsync();
        }

        private async Task LoadDashboardDataAsync()
        {
            try
            {
                // Fetch data (simulate API call)
                var dashboardData = await FetchAnalyticsDataAsync();

                // Update the ViewModel with fetched data
                // No need for Dispatcher.Invoke since we're already on UI thread for property updates
                _viewModel.ProductivityRate = $"{dashboardData.ProductivityRate:F0}%";
                _viewModel.TotalActivities = dashboardData.TotalActivities.ToString();
                _viewModel.RecentInterventions = dashboardData.RecentInterventions.ToString();

                // Clear existing data in TopApps before adding the new data
                _viewModel.TopApps.Clear();

                if (dashboardData.TopApps != null)
                {
                    // Add the fetched apps to the TopApps ObservableCollection, ordered by the app's value
                    foreach (var app in dashboardData.TopApps.OrderByDescending(x => x.Value))
                    {
                        _viewModel.TopApps.Add(app);
                    }
                }

                // Update count after adding items
                _viewModel.TopAppsCount = _viewModel.TopApps.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading dashboard data: {ex.Message}");
                // You could show an error message to the user here.
            }
        }

        // Simulated API response (replace with real API call)
        private Task<AnalyticsResponse> FetchAnalyticsDataAsync()
        {
            return Task.FromResult(new AnalyticsResponse
            {
                Date = "2025-09-07",
                ProductivityRate = 62.0,
                RecentInterventions = 456,
                TotalActivities = 500,
                TopApps = new Dictionary<string, int>
                {
                    { "Chrome", 109 },
                    { "Microsoft Word", 92 },
                    { "Slack", 101 },
                    { "Visual Studio Code", 109 },
                    { "YouTube", 89 }
                }
            });
        }
    }
}