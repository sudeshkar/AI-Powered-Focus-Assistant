using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace FocusAssistant
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private TrackingView _trackingViewInstance;
        private WindowTracker _windowTracker;

        public MainWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            try
            {
                var trackingView = serviceProvider.GetRequiredService<TrackingView>();
                Content = trackingView;
                Console.WriteLine($"MainWindow initialized with TrackingView at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MainWindow initialization failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                throw;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"MainWindow Loaded at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MainWindow Loaded failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"MainWindow load failed: {ex.Message}\nDetails: {ex.StackTrace}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MainWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                // Ensure tracking is stopped when window closes
                if (_windowTracker != null && _windowTracker.IsTracking)
                {
                    await _windowTracker.StopTrackingAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during window close: {ex.Message}");
            }
        }

        private void ShowDashboard(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"ShowDashboard called at {DateTime.Now:HH:mm:ss.fff}");
                var dashboardView = _serviceProvider.GetRequiredService<DashboardView>();
                MainContentFrame.Content = dashboardView;
                Console.WriteLine($"DashboardView set as content at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowDashboard failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"ShowDashboard failed: {ex.Message}\nDetails: {ex.StackTrace}", "Dashboard Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowTracking(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"ShowTracking called at {DateTime.Now:HH:mm:ss.fff}");
                if (_trackingViewInstance == null)
                {
                    _trackingViewInstance = _serviceProvider.GetRequiredService<TrackingView>();

                    // Wire up AI intervention events
                    _windowTracker.AiInterventionReceived += (s, response) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _trackingViewInstance?.AiInterventionReceived(response);
                        });
                    };
                }
                MainContentFrame.Content = _trackingViewInstance;
                Console.WriteLine($"TrackingView set as content at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowTracking failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"ShowTracking failed: {ex.Message}\nDetails: {ex.StackTrace}", "Tracking Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowAnalytics(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"ShowAnalytics called at {DateTime.Now:HH:mm:ss.fff}");
                var analyticsView = _serviceProvider.GetRequiredService<AnalyticsView>();
                MainContentFrame.Content = analyticsView;
                Console.WriteLine($"AnalyticsView set as content at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowAnalytics failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"ShowAnalytics failed: {ex.Message}\nDetails: {ex.StackTrace}", "Analytics Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowGamification(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"ShowGamification called at {DateTime.Now:HH:mm:ss.fff}");
                MainContentFrame.Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Achievements View\n\nComing soon...",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                Console.WriteLine($"Gamification content set at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowGamification failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"ShowGamification failed: {ex.Message}\nDetails: {ex.StackTrace}", "Gamification Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowRecommendations(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"ShowRecommendations called at {DateTime.Now:HH:mm:ss.fff}");
                MainContentFrame.Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Recommendations View\n\nComing soon...",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                Console.WriteLine($"Recommendations content set at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowRecommendations failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"ShowRecommendations failed: {ex.Message}\nDetails: {ex.StackTrace}", "Recommendations Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"ShowSettings called at {DateTime.Now:HH:mm:ss.fff}");
                MainContentFrame.Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Settings View\n\nComing soon...",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                Console.WriteLine($"Settings content set at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShowSettings failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"ShowSettings failed: {ex.Message}\nDetails: {ex.StackTrace}", "Settings Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}