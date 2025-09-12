using FocusAssistant.Enums;
using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using System;
using System.Windows;
using System.Windows.Threading;
using FocusAssistant.Services.Flask.Interfaces;

namespace FocusAssistant.Views
{
    public partial class AiInterventionWindow : Window
    {
        private readonly ActivityResponse _activityResponse;
        private readonly IFeedbackService _feedbackService;
        private readonly DispatcherTimer _timeoutTimer;
        private readonly string _appName;
        private readonly string _windowTitle;
        private readonly bool _isProductive;
        private const int TimeoutSeconds = 30;
        private bool _feedbackSkippedLogged;  // Throttle skip logs.
        private bool _isClosingFromAction;    // Flag for action-initiated close.

        public event EventHandler<AiUserAction> UserActionSelected;

        public AiInterventionWindow(ActivityResponse response, IFeedbackService feedbackService)
        {
            InitializeComponent();

            // Initialize timer for auto-close
            _timeoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(TimeoutSeconds)
            };
            _timeoutTimer.Tick += TimeoutTimer_Tick;
            _timeoutTimer.Start();

            LoadData(response);
            _feedbackService = feedbackService;
            _activityResponse = response;
            _feedbackSkippedLogged = false;  // Init throttle.
        }

        // FIX: Simplified - Remove non-existent 'IsCancelable'. Always attempt to cancel manual closes.
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosingFromAction)
            {
                e.Cancel = true;
                Console.WriteLine("Manual close prevented; use action buttons.");
            }
            // If _isClosingFromAction is true, allow close without canceling.
        }

        private async void TimeoutTimer_Tick(object sender, EventArgs e)
        {
            _timeoutTimer.Stop();
            await SendFeedbackAsync(AiUserAction.Ignored);
            RaiseUserAction(AiUserAction.Ignored);
        }

        private void RaiseUserAction(AiUserAction action)
        {
            _isClosingFromAction = true;  // Set flag to allow close.
            UserActionSelected?.Invoke(this, action);
            this.Close();  // Now proceeds without cancellation.
            _isClosingFromAction = false;  // Reset flag after close (optional, for safety).
        }

        public void UpdateContent(ActivityResponse response)
        {
            if (response == null) return;
            _timeoutTimer.Stop();
            LoadData(response);
            _timeoutTimer.Start();
            _feedbackSkippedLogged = false;  // Reset throttle on update.
            _isClosingFromAction = false;    // Reset flag on update.
        }

        private void LoadData(ActivityResponse response)
        {
            AiMessageTextBlock.Text = response.InterventionMessage ?? "No message available.";
            var detected = $"Detected Activity: {response.DistractionRisk:F1}";
            var suggested = $"Suggested Action: {response.InterventionMessage ?? "None"}";
            var fullSuggestion = $"{detected}\n\n{suggested}";
            if (fullSuggestion.Length > 150)
            {
                fullSuggestion = fullSuggestion.Substring(0, 147) + "...";
            }
            SuggestionText.Text = fullSuggestion;
        }

        // Add throttling for skip logs.
        private async Task SendFeedbackAsync(AiUserAction action)
        {
            if (_activityResponse?.InterventionId == null)
            {
                if (!_feedbackSkippedLogged)
                {
                    Console.WriteLine("Skipping feedback: InterventionId is null");
                    _feedbackSkippedLogged = true;
                }
                return;
            }
            _feedbackSkippedLogged = false;

            var feedback = new FeedbackRequest
            {
                InterventionId = _activityResponse.InterventionId,
                Action = action.ToString(),
                Helpful = action == AiUserAction.ActedImmediately || action == AiUserAction.ActedLater,
                ProductivityChange = 0,
            };
            try
            {
                await _feedbackService.SendFeedbackAsync(feedback);
                Console.WriteLine($"Feedback sent: InterventionId={feedback.InterventionId}, Action={feedback.Action}, Helpful={feedback.Helpful}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send feedback: {ex.Message}");
            }
        }

        private async void ActNow_Click(object sender, RoutedEventArgs e)
        {
            _timeoutTimer.Stop();
            await SendFeedbackAsync(AiUserAction.ActedImmediately);
            RaiseUserAction(AiUserAction.ActedImmediately);
        }

        private async void ActLater_Click(object sender, RoutedEventArgs e)
        {
            _timeoutTimer.Stop();
            await SendFeedbackAsync(AiUserAction.ActedLater);
            RaiseUserAction(AiUserAction.ActedLater);
        }

        private async void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            _timeoutTimer.Stop();
            await SendFeedbackAsync(AiUserAction.DismissedPolitely);
            RaiseUserAction(AiUserAction.DismissedPolitely);
        }

        private async void Ignore_Click(object sender, RoutedEventArgs e)
        {
            _timeoutTimer.Stop();
            await SendFeedbackAsync(AiUserAction.Ignored);
            RaiseUserAction(AiUserAction.Ignored);
        }
    }
}