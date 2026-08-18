using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FocusAssistant
{
    /// <summary>Shell window: hosts the navigation chrome and swaps views into the frame.</summary>
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly WindowTracker _windowTracker;

        public MainWindow(IServiceProvider serviceProvider, WindowTracker windowTracker)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));

            InitializeComponent();

            // Database creation moved to App startup: a constructor is the wrong
            // place for it, and a failure there left a half-built window.
            MainContentFrame.Content = _serviceProvider.GetRequiredService<TrackingView>();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_windowTracker.IsTracking)
                    await _windowTracker.StartTrackingAsync();
            }
            catch (Exception ex)
            {
                ShowError("start tracking", ex);
            }
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_windowTracker.IsTracking)
                    await _windowTracker.StopTrackingAsync();
            }
            catch (Exception ex)
            {
                // The window is already gone; log rather than raising more UI.
                Console.WriteLine($"Error stopping tracking on close: {ex.Message}");
            }
        }

        #region Navigation

        private void ShowDashboard(object sender, RoutedEventArgs e) => Navigate<DashboardView>("Dashboard");

        private void ShowTracking(object sender, RoutedEventArgs e) => Navigate<TrackingView>("Tracking");

        private void ShowAnalytics(object sender, RoutedEventArgs e) => Navigate<AnalyticsView>("Analytics");

        private void ShowRecommendations(object sender, RoutedEventArgs e) => Navigate<RecommendationsView>("Recommendations");

        private void ShowGamification(object sender, RoutedEventArgs e) => ShowPlaceholder("Achievements");

        private void ShowSettings(object sender, RoutedEventArgs e) => ShowPlaceholder("Settings");

        /// <summary>
        /// Resolves a view from the container and shows it. Views registered as
        /// singletons are reused, so navigating away and back keeps their state.
        /// </summary>
        private void Navigate<TView>(string name) where TView : notnull
        {
            try
            {
                MainContentFrame.Content = _serviceProvider.GetRequiredService<TView>();
            }
            catch (Exception ex)
            {
                ShowError($"load {name}", ex);
            }
        }

        private void ShowPlaceholder(string name)
        {
            MainContentFrame.Content = new TextBlock
            {
                Text = $"{name}\nComing soon",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
        }

        private static void ShowError(string action, Exception ex)
        {
            Console.WriteLine($"Failed to {action}: {ex}");
            MessageBox.Show($"Could not {action}.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion
    }
}
