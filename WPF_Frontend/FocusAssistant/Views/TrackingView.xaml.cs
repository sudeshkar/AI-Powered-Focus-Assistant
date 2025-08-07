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
    /// 

    public partial class TrackingView : UserControl
    {
        private WindowTracker _windowTracker;
        private LoggingService _loggingService;
        private DispatcherTimer _updateTimer;
        private ObservableCollection<ActivityLogItem> _activityLog;
        private DateTime _sessionStartTime;
        private IdleTimeDetector _idleDetector;
        private SessionManager _sessionManager;
        private DispatcherTimer _bannerTimer;
        private string _currentInterventionId;
        private readonly FlaskIntegrationService _flask = new();

        public TrackingView()
        {
            InitializeComponent();
            InitializeServices();
            InitializeUI();
        }

        private void InitializeServices()
        {
            _loggingService = new LoggingService();
            _idleDetector = new IdleTimeDetector(300); // 5 minutes
            _sessionManager = new SessionManager(_loggingService, _idleDetector);
            _windowTracker = new WindowTracker(_loggingService, _idleDetector, _sessionManager);

            // Subscribe to events
            _windowTracker.AppSwitched += OnAppSwitched;
            _windowTracker.SessionCompleted += OnSessionCompleted;
            _idleDetector.IdleStateChanged += OnIdleStateChanged;
            _idleDetector.IdleTimeUpdated += OnIdleTimeUpdated;
            _sessionManager.SessionUpdated += OnSessionUpdated;

            // Timer to update current activity duration
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(1);
            _updateTimer.Tick += UpdateCurrentActivity;
        }
        private void OnIdleStateChanged(object sender, IdleStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.IsIdle)
                {
                    IdleStatusBorder.Background = System.Windows.Media.Brushes.Orange;
                    IdleStatusText.Text = "😴 Idle";
                }
                else
                {
                    IdleStatusBorder.Background = System.Windows.Media.Brushes.Green;
                    IdleStatusText.Text = "🟢 Active";
                }

                // Save idle event
                _loggingService.SaveIdleLog(e);
            });
        }

        private void OnIdleTimeUpdated(object sender, TimeSpan idleTime)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentIdleTimeText.Text = FormatDuration(idleTime);
            });
        }

        private void OnSessionUpdated(object sender, WorkSession session)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateEnhancedSessionStats(session);
            });
        }

        private void UpdateEnhancedSessionStats(WorkSession session)
        {
            if (session == null) return;

            ProductivityScoreText.Text = $"{session.ProductivityScore:F0}%";
            TotalBreakTimeText.Text = FormatDuration(session.BreakTime);
            SessionLengthText.Text = FormatDuration(DateTime.Now - session.StartTime);

            // Update focus ratio
            var totalActiveTime = session.ProductiveTime + session.DistractedTime;
            if (totalActiveTime.TotalMinutes > 0)
            {
                var focusMinutes = (int)session.ProductiveTime.TotalMinutes;
                var distractedMinutes = (int)session.DistractedTime.TotalMinutes;
                FocusRatioText.Text = $"{focusMinutes}:{distractedMinutes}";
            }
            else
            {
                FocusRatioText.Text = "0:0";
            }
        }

        private void ExportData(object sender, RoutedEventArgs e)
        {
            try
            {
                var sessions = _loggingService.LoadWorkSessions(30); // Last 30 days
                if (!sessions.Any())
                {
                    MessageBox.Show("No session data available to export.",
                                  "Export Data",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"focus_assistant_data_{DateTime.Now:yyyy-MM-dd}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    _loggingService.ExportToCsv(sessions, saveDialog.FileName);
                    MessageBox.Show($"Data exported successfully!\n\nFile: {saveDialog.FileName}\nSessions: {sessions.Count}",
                                  "Export Complete",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting data: {ex.Message}",
                              "Export Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void AiYes_Click(object sender, RoutedEventArgs e) => SendFeedback(true, "acted");
        private void AiNo_Click(object sender, RoutedEventArgs e) => SendFeedback(false, "ignored");
        private void AiIgnore_Click(object sender, RoutedEventArgs e) => SendFeedback(false, "dismissed");

        private async void SendFeedback(bool helpful, string action)
        {
            if (string.IsNullOrEmpty(_currentInterventionId)) return;

            await _flask.SendFeedbackAsync(helpful, action, interventionId: _currentInterventionId);
            AiBanner.Visibility = Visibility.Collapsed;
            _bannerTimer?.Stop();
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
            StatusText.Text = "Actively tracking your application usage and idle time...";

            // Reset UI elements
            IdleStatusBorder.Background = System.Windows.Media.Brushes.Green;
            IdleStatusText.Text = "🟢 Active";
            CurrentIdleTimeText.Text = "00:00";
            TotalBreakTimeText.Text = "00:00";
            ProductivityScoreText.Text = "0%";
            FocusRatioText.Text = "0:0";
            SessionLengthText.Text = "00:00:00";

            UpdateSessionSummary();
        }

        private void StopTracking()
        {
            _windowTracker.StopTracking();
            _updateTimer.Stop();

            TrackingButton.Content = "🔴 Start Tracking";
            TrackingButton.Background = System.Windows.Media.Brushes.Red;
            StatusText.Text = "Tracking stopped. Session data saved with idle time analysis.";

            CurrentAppText.Text = "Not tracking";
            CurrentWindowText.Text = "N/A";
            CurrentDurationText.Text = "00:00:00";
            ProductivityIcon.Text = "⚪";
            ProductivityText.Text = "Idle";

            IdleStatusBorder.Background = System.Windows.Media.Brushes.Gray;
            IdleStatusText.Text = "⚪ Stopped";
        }

        private void OnAppSwitched(object sender, AppUsage usage)
        {
            Dispatcher.Invoke(async () =>
            {
                // 1. Send activity to AI
                var resp = await _flask.SendActivityAsync(usage);

                // 2. Show banner only if we got a message
                if (!string.IsNullOrEmpty(resp.InterventionMessage) &&
                    resp.InterventionId != _currentInterventionId)
                {
                    _currentInterventionId = resp.InterventionId;
                    AiMessageText.Text = resp.InterventionMessage;
                    AiBanner.Visibility = Visibility.Visible;

                    // restart 10-second clock
                    _bannerTimer?.Stop();
                    _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                    _bannerTimer.Tick += (_, _) =>
                    {
                        AiBanner.Visibility = Visibility.Collapsed;
                        _bannerTimer.Stop();
                    };
                    _bannerTimer.Start();
                }

                // 3. Log in list (existing code)
                var logItem = new ActivityLogItem
                {
                    AppName = usage.AppName,
                    WindowTitle = usage.WindowTitle,
                    Duration = usage.Duration,
                    DurationText = FormatDuration(usage.Duration),
                    TimeText = usage.StartTime.ToString("HH:mm:ss"),
                    IsProductive = usage.IsProductive,
                    ProductivityIcon = usage.IsProductive ? "🟢" : "🔴"
                };
                _activityLog.Insert(0, logItem);
                while (_activityLog.Count > 20) _activityLog.RemoveAt(_activityLog.Count - 1);

                UpdateSessionSummary();
                _loggingService.SaveRealTimeLog(usage);
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
