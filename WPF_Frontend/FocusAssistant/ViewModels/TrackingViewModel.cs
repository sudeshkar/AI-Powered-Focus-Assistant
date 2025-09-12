using FocusAssistant.Enums;
using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Interfaces;
using FocusAssistant.Services.Models.Events;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.Views;
using MailChimp.Net.Models;
using OpenTK.Platform;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FocusAssistant.ViewModels
{
    public class TrackingViewModel : INotifyPropertyChanged,IDisposable
    {
        private bool _isTracking;
        private string _statusText;
        private string _productivityRate;
        private string _recentInterventions;
        private string _productivityScore;
        private string _aiStatus;
        private string _date;
        private List<string> _topApps;
        private string _totalActivities;
        private string _currentApp;
        private string _currentWindow;
        private string _windowStatusText;
        public ActivityResponse AiIntervention { get; private set; }

        private readonly IAnalyticsService _analyticsService;
        private readonly IReportGenerator _reportGenerator;
        private readonly WindowTracker _windowTracker;
        private readonly IWindowMonitor _windowMonitor;
        private readonly ISessionManager _sessionManager;
        public event EventHandler<ActivityResponse>? AiInterventionOccurred;
        private readonly IFeedbackService _feedbackService;

        public TrackingViewModel(
            WindowTracker windowTracker,
            IAnalyticsService analyticsService,
            IReportGenerator reportGenerator,
            IWindowMonitor windowMonitor,
            ISessionManager sessionManager,IFeedbackService feedbackService)
        {
            _windowTracker = windowTracker;
            _analyticsService = analyticsService;
            _reportGenerator = reportGenerator;
            _windowMonitor = windowMonitor;
            _sessionManager = sessionManager;
            _feedbackService = feedbackService;

            // Initialize collections
            ActivityLog = new ObservableCollection<ActivityLogItem>();

            // Initialize commands
            StartTrackingCommand = new AsyncRelayCommand(StartTrackingAsync);
            StopTrackingCommand = new AsyncRelayCommand(StopTrackingAsync);

            // Initialize default values
            StatusText = "Ready to track";
            ProductivityScore = "0.0%";
            RecentInterventions = "0h";
            AIStatus = "Ready";
            TotalActivities = "0";
            CurrentApp = "No application detected";
            CurrentWindow = "No window detected";

          
            
            _windowMonitor.WindowChanged += OnWindowChanged;
            _sessionManager.AiInterventionReceived += OnAiInterventionReceived;
           
        }

        private void OnAiInterventionReceived(object? sender, ActivityResponse e)
        {
            // Early skip for invalid responses to prevent empty popups/logs
            if (e == null || e.InterventionId == null || string.IsNullOrEmpty(e.InterventionMessage))
            {
                Console.WriteLine("Skipping intervention: Invalid response (null ID or empty message)");
                return;
            }

            AiInterventionOccurred?.Invoke(this, e);  // Forward to UI layer for handling
        }


        public void HandleUserAction(ActivityResponse e, AiUserAction action)
        {
            Console.WriteLine($"User responded: {action} for '{e.InterventionMessage ?? "No message"}'");
            // TODO: Send this data to your backend or RL service
            // Example:
            // await _sessionManager.SaveUserActionAsync(response, action);

            // Optional: Update UI
            // _viewModel.StatusText = $"Last action: {action}";
        }

        #region Properties
        public bool IsTracking
        {
            get => _isTracking;
            set { _isTracking = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string ProductivityScore
        {
            get => _productivityScore;
            set { _productivityScore = value; OnPropertyChanged(); }
        }

        public string RecentInterventions
        {
            get => _recentInterventions;
            set { _recentInterventions = value; OnPropertyChanged(); }
        }

        public string AIStatus
        {
            get => _aiStatus;
            set { _aiStatus = value; OnPropertyChanged(); }
        }

        public string TotalActivities
        {
            get => _totalActivities;
            set { _totalActivities = value; OnPropertyChanged(); }
        }

        public string CurrentApp
        {
            get => _currentApp;
            set { _currentApp = value; OnPropertyChanged(); }
        }

        public string CurrentWindow
        {
            get => _currentWindow;
            set { _currentWindow = value; OnPropertyChanged(); }
        }

        public string WindowStatus
        {
            get => _windowStatusText;
            set { _windowStatusText = value; OnPropertyChanged(); }
        }

        public string Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged(); }
        }

        public List<string> TopApps
        {
            get => _topApps;
            set { _topApps = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ActivityLogItem> ActivityLog { get; }
        #endregion

        #region Commands
        public ICommand StartTrackingCommand { get; }
        public ICommand StopTrackingCommand { get; }
        #endregion

        #region Command Handlers
        private async Task StartTrackingAsync()
        {
            try
            {
                StatusText = "Actively tracking...";
                IsTracking = true;
                AIStatus = "Active";
                await _windowTracker.StartTrackingAsync();

                // Load initial analytics
               // await LoadAnalyticsAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                AIStatus = "Error";
            }
        }

        private async Task StopTrackingAsync()
        {
            try
            {
                StatusText = "Tracking stopped";
                IsTracking = false;
                AIStatus = "Stopped";
                await _windowTracker.StopTrackingAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                AIStatus = "Error";
            }
        }
        #endregion

        #region Analytics Loading
        public async Task LoadAnalyticsAsync()
        {
            try
            {
                
                var report = await _reportGenerator.GetReportFlask();

                ProductivityScore = $"{report.ProductivityRate:F1}%";
                RecentInterventions = $"{report.RecentInterventions:F1}h";
                AIStatus = report.Status ?? "Active";
                Date = report.Date;
                TopApps = report.TopApps ?? new List<string>();
                TotalActivities = ActivityLog.Count.ToString();
                Console.WriteLine("Flask request loaded");
                
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading analytics: {ex.Message}";
                AIStatus = "Error";
                Console.WriteLine("Flask request loaded failed");
            }
        }
        #endregion

        #region Event Handlers
        private void OnAppSwitched(object sender, AppUsage app)
        {
            CurrentApp = app.AppName;

            ActivityLog.Add(new ActivityLogItem
            {
                AppName = app.AppName,
                DurationText = $"{app.Duration.TotalMinutes:F1} min"
            });

            // Update total activities count
            TotalActivities = ActivityLog.Count.ToString();

            // Update status
            StatusText = $"Switched to {app.AppName}";
        }

        private void 
            OnWindowChanged(object sender, AppWindowChangedEventArgs window)
        {
            CurrentWindow = window.CurrentWindowTitle;
            CurrentApp = window.CurrentAppName;
            
            StatusText = $"Active window: {window.CurrentWindowTitle}";
        }
        #endregion

        #region Public Methods
        public async void RefreshDataAsync()
        {
            await LoadAnalyticsAsync();
        }

        public void ClearActivityLog()
        {
            ActivityLog.Clear();
            TotalActivities = "0";
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            _windowMonitor.StopMonitoring();
            _windowMonitor.WindowChanged -= OnWindowChanged;
        }
    }

    public class ActivityLogItem
    {
        public string AppName { get; set; }
        public string DurationText { get; set; }
    }

}