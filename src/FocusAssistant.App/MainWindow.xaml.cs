using FocusAssistant.Core.Monitoring;
using FocusAssistant.Hosting;
using FocusAssistant.Views;
using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace FocusAssistant
{
    /// <summary>Shell window: hosts the Fluent navigation chrome and the page it shows.</summary>
    /// <remarks>
    /// Navigation is the control's own rather than a hand-rolled switch. Each menu item
    /// names its TargetPageType and the NavigationView resolves it through the service
    /// provider, so pages arrive with their view models already injected.
    /// <para>
    /// The previous version kept a Frame inside NavigationView.ContentOverlay and assigned
    /// to it from a SelectionChanged handler that read SelectedItem. Two things were wrong
    /// with that, and together they made the whole pane inert: ContentOverlay draws *over*
    /// the navigation content rather than being it, and SelectedItem is only populated once
    /// a real navigation has happened - which, with no TargetPageType on any item, it never
    /// had. Every click read a null SelectedItem and returned early.
    /// </para>
    /// </remarks>
    public partial class MainWindow : FluentWindow
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly WindowTracker _windowTracker;
        private readonly StartupState _startupState;

        public MainWindow(IServiceProvider serviceProvider, WindowTracker windowTracker, StartupState startupState)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));

            InitializeComponent();
        }

        private async void RootNavigation_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Must come before the first Navigate: without it the control has no way to
                // build pages whose constructors take dependencies, and navigation fails on
                // a missing parameterless constructor.
                RootNavigation.SetServiceProvider(_serviceProvider);
                RootNavigation.Navigate(typeof(DashboardView));
            }
            catch (Exception ex)
            {
                ShowError("open the dashboard", ex);
            }

            try
            {
                // Migrations run on a background thread so the window can paint at once,
                // which means the schema may not exist yet. Starting a session before it
                // does fails on "no such table: UserSessions" - only invisibly, on a first
                // run, where the app looks like it is working and records nothing.
                if (!await _startupState.DatabaseReady)
                    return;

                if (!_windowTracker.IsTracking)
                    await _windowTracker.StartTrackingAsync();
            }
            catch (Exception ex)
            {
                ShowError("start tracking", ex);
            }
        }

        /// <summary>
        /// True once the app is genuinely exiting, as opposed to the window being closed.
        /// </summary>
        public bool IsClosingForReal { get; set; }

        /// <summary>
        /// Closing the window hides it instead of ending the day.
        /// </summary>
        /// <remarks>
        /// Tracking belongs to the process now, not to this window, so closing must not stop
        /// it - that was the old behaviour and it made the app useless the moment you tidied
        /// your taskbar. Quit on the tray menu is the way out, and it sets
        /// <see cref="IsClosingForReal"/> first.
        /// </remarks>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsClosingForReal)
                return;

            e.Cancel = true;
            Hide();
        }

        private static void ShowError(string action, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to {action}: {ex}");
            System.Windows.MessageBox.Show($"Could not {action}.\n\n{ex.Message}",
                "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
