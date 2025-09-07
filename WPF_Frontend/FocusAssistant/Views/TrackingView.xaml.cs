using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.ViewModels;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.UI.Notifications;

namespace FocusAssistant.Views
{
    public partial class TrackingView : UserControl
    {
        private readonly TrackingViewModel _viewModel;
        private readonly WindowTracker _windowTracker;
        private readonly IActivityService _activityService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IFeedbackService _feedbackService;
        private readonly IReportGenerator _reportGenerator;
        private readonly ISessionManager _sessionManager;

        public TrackingView(
            TrackingViewModel viewModel,
            WindowTracker windowTracker,
            IActivityService activityService,
            IAnalyticsService analyticsService,
            IFeedbackService feedbackService,
            IReportGenerator reportGenerator,
            ISessionManager sessionManager)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _windowTracker = windowTracker;
            _activityService = activityService;
            _analyticsService = analyticsService;
            _feedbackService = feedbackService;
            _reportGenerator = reportGenerator;
            _sessionManager = sessionManager;
            DataContext = _viewModel;

            _windowTracker.AppSwitched += OnAppSwitched;
            _windowTracker.AiInterventionReceived += OnAiInterventionReceived;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var analytics = await _analyticsService.GetAnalyticsAsync();
                _viewModel.ProductivityScore = $"{analytics.ProductivityRate:F1}%";
                var report = await _reportGenerator.GenerateReportAsync(DateTime.Today);
                _viewModel.ProductiveTime = $"{report.ProductiveTime.TotalHours:F1}h";
                _viewModel.DistractedTime = $"{report.DistractedTime.TotalHours:F1}h";
                _viewModel.ProductivityStreak = $"{report.ProductivityStreak} days";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading analytics: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
            }
        }

        private void OnAppSwitched(object sender, AppUsage app)
        {
            _viewModel.ActivityLog.Add(new FocusAssistant.ViewModels.ActivityLogItem
            {
                AppName = app.AppName,
                DurationText = $"{app.Duration.TotalMinutes:F1} min"
            });
        }

        private async void OnAiInterventionReceived(object sender, ActivityResponse response)
        {
            if (!string.IsNullOrEmpty(response.InterventionMessage))
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    var toast = new ToastNotification
                    {
                        Message = response.InterventionMessage,
                        ButtonText = response.ActionTaken
                    };
                    toast.ButtonClicked += async (s, e) =>
                    {
                        await _feedbackService.SendFeedbackAsync(new FeedbackRequest
                        {
                            ActionTaken = response.ActionTaken,
                            Timestamp = DateTime.Now
                        });
                        Console.WriteLine($"Feedback sent: {response.ActionTaken} at {DateTime.Now:HH:mm:ss.fff}");
                    };
                    toast.Show();
                });
            }
        }

        private async void StartTracking_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.StatusText = "Actively tracking...";
                _sessionManager.StartSession();
                await _windowTracker.StartTrackingAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting tracking: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
            }
        }

        private async void StopTracking_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.StatusText = "Tracking stopped";
                await Task.Run(async () => await _windowTracker.StopTrackingAsync());
                await _sessionManager.EndSessionAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping tracking: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
            }
        }
    }
}