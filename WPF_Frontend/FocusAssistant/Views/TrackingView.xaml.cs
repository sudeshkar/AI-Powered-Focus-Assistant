using FocusAssistant.Models;
using FocusAssistant.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FocusAssistant.Views
{
    /// <summary>
    /// Interaction logic for TrackingView.xaml
    /// </summary>
    public partial class TrackingView : UserControl
    {
        private WindowTracker _windowTracker;
        private LoggingService _loggingService;
        private DispatcherTimer _updateTimer;
        private ObservableCollection<ActivityLogItem> _activityLog;
        private DateTime _sessionStartTime;

        public TrackingView()
        {
            InitializeComponent();
            InitializeServices();
            InitializeUI();
        }

        private void InitializeServices()
        {
            _loggingService = new LoggingService();
            _windowTracker = new WindowTracker(_loggingService);

            // Subscribe to events
            _windowTracker.AppSwitched += OnAppSwitched;
            _windowTracker.SessionCompleted += OnSessionCompleted;

            // Timer to update current activity duration
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(1);
            _updateTimer.Tick += UpdateCurrentActivity;
        }

        private void InitializeUI()
        {
            _activityLog = new ObservableCollection<ActivityLogItem>();
            ActivityListView.ItemsSource = _activityLog;
        }

        private void ToggleTracking(object sender, RoutedEventArgs e)
        {
            if (_windowTracker.IsTracking)
            {
                StopTracking();
            }
            else
            {
                StartTracking();
            }
        }

        private void StartTracking()
        {
            _sessionStartTime = DateTime.Now;
            _activityLog.Clear();

            _windowTracker.StartTracking();
            _updateTimer.Start();

            TrackingButton.Content = "⏹️ Stop Tracking";
            TrackingButton.Background = System.Windows.Media.Brushes.Green;
            StatusText.Text = "Actively tracking your application usage...";

            UpdateSessionSummary();
        }

        private void StopTracking()
        {
            _windowTracker.StopTracking();
            _updateTimer.Stop();

            TrackingButton.Content = "🔴 Start Tracking";
            TrackingButton.Background = System.Windows.Media.Brushes.Red;
            StatusText.Text = "Tracking stopped. Session data saved.";

            CurrentAppText.Text = "Not tracking";
            CurrentWindowText.Text = "N/A";
            CurrentDurationText.Text = "00:00:00";
            ProductivityIcon.Text = "⚪";
            ProductivityText.Text = "Idle";
        }

        private void OnAppSwitched(object sender, AppUsage appUsage)
        {
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                var logItem = new ActivityLogItem
                {
                    AppName = appUsage.AppName,
                    WindowTitle = appUsage.WindowTitle,
                    Duration = appUsage.Duration,
                    DurationText = FormatDuration(appUsage.Duration),
                    TimeText = appUsage.StartTime.ToString("HH:mm:ss"),
                    IsProductive = appUsage.IsProductive,
                    ProductivityIcon = appUsage.IsProductive ? "🟢" : "🔴"
                };

                _activityLog.Insert(0, logItem);

                // Keep only last 20 items in UI
                while (_activityLog.Count > 20)
                {
                    _activityLog.RemoveAt(_activityLog.Count - 1);
                }

                UpdateSessionSummary();

                // Save real-time log
                _loggingService.SaveRealTimeLog(appUsage);
            });
        }

        private void OnSessionCompleted(object sender, System.Collections.Generic.List<AppUsage> session)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Session completed!\n\n" +
                              $"Total apps used: {session.Count}\n" +
                              $"Session duration: {FormatDuration(TimeSpan.FromMinutes(session.Sum(a => a.Duration.TotalMinutes)))}\n" +
                              $"Data saved successfully.",
                              "Session Complete",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            });
        }

        private void UpdateCurrentActivity(object sender, EventArgs e)
        {
            if (!_windowTracker.IsTracking) return;

            // This would need access to current app info from WindowTracker
            // For now, we'll update the session duration
            var sessionDuration = DateTime.Now - _sessionStartTime;
            // Update any real-time displays here
        }

        private void UpdateSessionSummary()
        {
            if (_activityLog.Count == 0) return;

            var totalTime = _activityLog.Sum(a => a.Duration.TotalMinutes);
            var productiveTime = _activityLog.Where(a => a.IsProductive).Sum(a => a.Duration.TotalMinutes);
            var distractedTime = totalTime - productiveTime;

            TotalTimeText.Text = FormatDuration(TimeSpan.FromMinutes(totalTime));
            ProductiveTimeText.Text = FormatDuration(TimeSpan.FromMinutes(productiveTime));
            DistractedTimeText.Text = FormatDuration(TimeSpan.FromMinutes(distractedTime));
            AppSwitchesText.Text = _activityLog.Count.ToString();
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            else
                return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
        }
    }

    public class ActivityLogItem
    {
        public string AppName { get; set; }
        public string WindowTitle { get; set; }
        public TimeSpan Duration { get; set; }
        public string DurationText { get; set; }
        public string TimeText { get; set; }
        public bool IsProductive { get; set; }
        public string ProductivityIcon { get; set; }
    }
}
