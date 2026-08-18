using FocusAssistant.Core.Monitoring;
using FocusAssistant.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace FocusAssistant
{
    /// <summary>Shell window: hosts the Fluent navigation chrome and swaps views into the frame.</summary>
    public partial class MainWindow : FluentWindow
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly WindowTracker _windowTracker;

        public MainWindow(IServiceProvider serviceProvider, WindowTracker windowTracker)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));

            InitializeComponent();

            // Shown directly rather than driven through NavigationView's own
            // selection, since SelectedItem has no accessible setter on this
            // control. The nav rail highlights an item as soon as the user clicks
            // one; nothing depends on one being pre-highlighted at startup.
            MainContentFrame.Content = _serviceProvider.GetRequiredService<DashboardView>();
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

        private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
        {
            var tag = (sender.SelectedItem as NavigationViewItem)?.Tag as string;
            if (tag is null)
                return;

            try
            {
                MainContentFrame.Content = tag switch
                {
                    "Dashboard" => _serviceProvider.GetRequiredService<DashboardView>(),
                    "Tracking" => _serviceProvider.GetRequiredService<TrackingView>(),
                    "Analytics" => _serviceProvider.GetRequiredService<AnalyticsView>(),
                    "Recommendations" => _serviceProvider.GetRequiredService<RecommendationsView>(),
                    "Achievements" => Placeholder("Achievements"),
                    "Settings" => Placeholder("Settings"),
                    _ => MainContentFrame.Content,
                };
            }
            catch (Exception ex)
            {
                ShowError($"load {tag}", ex);
            }
        }

        private static System.Windows.Controls.TextBlock Placeholder(string name) => new System.Windows.Controls.TextBlock()
        {
            Text = $"{name}\nComing soon",
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        private static void ShowError(string action, Exception ex)
        {
            Console.WriteLine($"Failed to {action}: {ex}");
            System.Windows.MessageBox.Show($"Could not {action}.\n\n{ex.Message}",
                "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
