using FocusAssistant.Enums;
using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Models.Events;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FocusAssistant.ViewModels
{
    /// <summary>Backs the Tracking view: live status, the activity log, and today's totals.</summary>
    public class TrackingViewModel : ObservableObject, IDisposable
    {
        // The log is a live view, not storage; the database holds the full history.
        private const int MaxActivityLogEntries = 200;

        private readonly WindowTracker _windowTracker;
        private readonly IWindowMonitor _windowMonitor;
        private readonly ISessionManager _sessionManager;
        private readonly IReportGenerator _reportGenerator;

        private bool _isTracking;
        private string _statusText = "Ready to track";
        private string _productivityScore = "0.0%";
        private string _recentInterventions = "0";
        private string _aiStatus = "Ready";
        private string _totalActivities = "0";
        private string _currentApp = "No application detected";
        private string _currentWindow = "No window detected";
        private string _date = DateTime.Today.ToString("yyyy-MM-dd");
        private List<string> _topApps = new();
        private bool _disposed;

        public event EventHandler<ActivityResponse>? AiInterventionOccurred;

        public TrackingViewModel(
            WindowTracker windowTracker,
            IWindowMonitor windowMonitor,
            ISessionManager sessionManager,
            IReportGenerator reportGenerator)
        {
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));

            StartTrackingCommand = new AsyncRelayCommand(StartTrackingAsync, () => !IsTracking);
            StopTrackingCommand = new AsyncRelayCommand(StopTrackingAsync, () => IsTracking);

            _windowMonitor.WindowChanged += OnWindowChanged;
            _sessionManager.AiInterventionReceived += OnAiInterventionReceived;
        }

        #region Properties

        public bool IsTracking
        {
            get => _isTracking;
            set
            {
                if (!SetProperty(ref _isTracking, value))
                    return;

                (StartTrackingCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (StopTrackingCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public string ProductivityScore { get => _productivityScore; set => SetProperty(ref _productivityScore, value); }
        public string RecentInterventions { get => _recentInterventions; set => SetProperty(ref _recentInterventions, value); }
        public string AIStatus { get => _aiStatus; set => SetProperty(ref _aiStatus, value); }
        public string TotalActivities { get => _totalActivities; set => SetProperty(ref _totalActivities, value); }
        public string CurrentApp { get => _currentApp; set => SetProperty(ref _currentApp, value); }
        public string CurrentWindow { get => _currentWindow; set => SetProperty(ref _currentWindow, value); }
        public string Date { get => _date; set => SetProperty(ref _date, value); }
        public List<string> TopApps { get => _topApps; set => SetProperty(ref _topApps, value); }

        public ObservableCollection<ActivityLogItem> ActivityLog { get; } = new();

        public ICommand StartTrackingCommand { get; }
        public ICommand StopTrackingCommand { get; }

        #endregion

        private async Task StartTrackingAsync()
        {
            try
            {
                await _windowTracker.StartTrackingAsync();
                IsTracking = true;
                StatusText = "Actively tracking";
                AIStatus = "Active";
                await LoadAnalyticsAsync();
            }
            catch (Exception ex)
            {
                IsTracking = false;
                StatusText = $"Could not start tracking: {ex.Message}";
                AIStatus = "Error";
            }
        }

        private async Task StopTrackingAsync()
        {
            try
            {
                await _windowTracker.StopTrackingAsync();
                IsTracking = false;
                StatusText = "Tracking stopped";
                AIStatus = "Stopped";
                await LoadAnalyticsAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Could not stop tracking: {ex.Message}";
                AIStatus = "Error";
            }
        }

        /// <summary>Refreshes the headline figures from the backend or local history.</summary>
        public async Task LoadAnalyticsAsync()
        {
            try
            {
                var report = await _reportGenerator.GetReportFlask();

                ProductivityScore = $"{report.ProductivityRate:F1}%";
                // A count, not a duration. This used to be rendered as "0h".
                RecentInterventions = report.RecentInterventions.ToString();
                AIStatus = report.Status == "success" ? "Active" : "Unavailable";
                Date = report.Date ?? DateTime.Today.ToString("yyyy-MM-dd");
                TopApps = report.TopApps.Keys.ToList();
                TotalActivities = report.TotalActivities.ToString();
            }
            catch (Exception ex)
            {
                StatusText = $"Could not load analytics: {ex.Message}";
                AIStatus = "Error";
            }
        }

        private void OnAiInterventionReceived(object? sender, ActivityResponse e)
        {
            if (e?.InterventionId is null || string.IsNullOrEmpty(e.InterventionMessage))
                return;

            AiInterventionOccurred?.Invoke(this, e);
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

                TotalActivities = ActivityLog.Count.ToString();
            });
        }

        /// <summary>Records how the user answered an intervention.</summary>
        public void HandleUserAction(ActivityResponse response, AiUserAction action)
        {
            // Feedback is sent by AiInterventionWindow, which owns the intervention id.
            StatusText = $"Last suggestion: {action}";
            Console.WriteLine($"User responded {action} to '{response.InterventionMessage}'.");
        }

        public void ClearActivityLog()
        {
            ActivityLog.Clear();
            TotalActivities = "0";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // The monitor and session manager are singletons: without this the view
            // model stays reachable from them for the life of the process.
            _windowMonitor.WindowChanged -= OnWindowChanged;
            _sessionManager.AiInterventionReceived -= OnAiInterventionReceived;
        }
    }
}
