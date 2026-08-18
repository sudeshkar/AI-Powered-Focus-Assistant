using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusAssistant.Core.Models;
using FocusAssistant.Core.Monitoring;
using FocusAssistant.Core.Reports;
using FocusAssistant.Core.Session;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FocusAssistant.ViewModels
{
    /// <summary>Backs the Tracking view: live status, the activity log, and today's totals.</summary>
    public partial class TrackingViewModel : ObservableObject, IDisposable
    {
        // The log is a live view, not storage; the database holds the full history.
        private const int MaxActivityLogEntries = 200;

        private readonly WindowTracker _windowTracker;
        private readonly IWindowMonitor _windowMonitor;
        private readonly ISessionEngine _sessionEngine;
        private readonly IReportGenerator _reportGenerator;
        private bool _disposed;

        [ObservableProperty]
        private bool isTracking;

        [ObservableProperty]
        private string statusText = "Ready to track";

        [ObservableProperty]
        private string goal = string.Empty;

        [ObservableProperty]
        private string productivityScore = "0.0%";

        [ObservableProperty]
        private string recentInterventions = "0";

        [ObservableProperty]
        private string aiStatus = "Ready";

        [ObservableProperty]
        private string totalActivities = "0";

        [ObservableProperty]
        private string currentApp = "No application detected";

        [ObservableProperty]
        private string currentWindow = "No window detected";

        [ObservableProperty]
        private string date = DateTime.Today.ToString("yyyy-MM-dd");

        [ObservableProperty]
        private List<string> topApps = new();

        public ObservableCollection<ActivityLogItem> ActivityLog { get; } = new();

        public TrackingViewModel(
            WindowTracker windowTracker,
            IWindowMonitor windowMonitor,
            ISessionEngine sessionEngine,
            IReportGenerator reportGenerator)
        {
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _sessionEngine = sessionEngine ?? throw new ArgumentNullException(nameof(sessionEngine));
            _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));

            _windowMonitor.WindowChanged += OnWindowChanged;
        }

        partial void OnIsTrackingChanged(bool value)
        {
            StartTrackingCommand.NotifyCanExecuteChanged();
            StopTrackingCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanStartTracking))]
        private async Task StartTrackingAsync()
        {
            try
            {
                // WindowTracker starts the session itself (session first, then the
                // monitors) - calling StartSessionAsync here too would start two
                // sessions back to back.
                await _windowTracker.StartTrackingAsync(string.IsNullOrWhiteSpace(Goal) ? null : Goal);
                IsTracking = true;
                StatusText = string.IsNullOrEmpty(_sessionEngine.CurrentGoal)
                    ? "Actively tracking"
                    : $"Actively tracking - {_sessionEngine.CurrentGoal}";
                AiStatus = "Active";
                await LoadReportAsync();
            }
            catch (Exception ex)
            {
                IsTracking = false;
                StatusText = $"Could not start tracking: {ex.Message}";
                AiStatus = "Error";
            }
        }

        private bool CanStartTracking() => !IsTracking;

        [RelayCommand(CanExecute = nameof(CanStopTracking))]
        private async Task StopTrackingAsync()
        {
            try
            {
                await _windowTracker.StopTrackingAsync();
                IsTracking = false;
                StatusText = "Tracking stopped";
                AiStatus = "Stopped";
                await LoadReportAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Could not stop tracking: {ex.Message}";
                AiStatus = "Error";
            }
        }

        private bool CanStopTracking() => IsTracking;

        /// <summary>Refreshes the headline figures from local history.</summary>
        public async Task LoadReportAsync()
        {
            try
            {
                var report = await _reportGenerator.GetTodayReportAsync();

                ProductivityScore = $"{report.ProductivityRate:F1}%";
                RecentInterventions = report.RecentInterventions.ToString();
                AiStatus = report.Status == "success" ? "Active" : "Unavailable";
                Date = report.Date;
                TopApps = report.TopApps.Keys.ToList();
                TotalActivities = report.TotalActivities.ToString();
            }
            catch (Exception ex)
            {
                StatusText = $"Could not load report: {ex.Message}";
                AiStatus = "Error";
            }
        }

        private void OnWindowChanged(object? sender, AppWindowChangedEventArgs e)
        {
            // Raised on a polling thread; ObservableCollection must only be mutated
            // on the dispatcher thread or the ListBox binding throws.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
                return;

            dispatcher.InvokeAsync(() =>
            {
                CurrentApp = e.CurrentAppName;
                CurrentWindow = e.CurrentWindowTitle ?? string.Empty;
                StatusText = $"Active window: {e.CurrentWindowTitle}";

                ActivityLog.Insert(0, new ActivityLogItem
                {
                    AppName = e.CurrentAppName,
                    WindowTitle = e.CurrentWindowTitle ?? string.Empty,
                    TimeText = e.ChangeTime.ToString("HH:mm:ss"),
                    DurationText = "in progress",
                });

                while (ActivityLog.Count > MaxActivityLogEntries)
                    ActivityLog.RemoveAt(ActivityLog.Count - 1);
            });
        }

        public void ClearActivityLog() => ActivityLog.Clear();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // The monitor is a singleton: without this the view model stays
            // reachable from it for the life of the process.
            _windowMonitor.WindowChanged -= OnWindowChanged;
        }
    }
}
