using FocusAssistant.Enums;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FocusAssistant.Views
{
    /// <summary>
    /// Transient popup offering the agent's suggestion. Whatever the user does with
    /// it — including closing it — is reported back as feedback so the agent learns.
    /// </summary>
    public partial class AiInterventionWindow : Window
    {
        private const int TimeoutSeconds = 30;

        private readonly IFeedbackService _feedbackService;
        private readonly DispatcherTimer _timeoutTimer;

        private ActivityResponse _activityResponse;
        private bool _actionReported;

        public event EventHandler<AiUserAction>? UserActionSelected;

        public AiInterventionWindow(ActivityResponse response, IFeedbackService feedbackService)
        {
            ArgumentNullException.ThrowIfNull(response);

            InitializeComponent();

            _feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));
            _activityResponse = response;

            _timeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TimeoutSeconds) };
            _timeoutTimer.Tick += TimeoutTimer_Tick;
            _timeoutTimer.Start();

            LoadData(response);
        }

        /// <summary>
        /// Closing counts as ignoring the suggestion.
        /// </summary>
        /// <remarks>
        /// This used to cancel every close the user initiated, which trapped them in
        /// a window with no working close button and blocked application shutdown.
        /// </remarks>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timeoutTimer.Stop();

            if (!_actionReported)
                ReportAction(AiUserAction.Ignored, closeWindow: false);
        }

        private void TimeoutTimer_Tick(object? sender, EventArgs e) => Respond(AiUserAction.Ignored);

        private void ActNow_Click(object sender, RoutedEventArgs e) => Respond(AiUserAction.ActedImmediately);

        private void ActLater_Click(object sender, RoutedEventArgs e) => Respond(AiUserAction.ActedLater);

        private void Dismiss_Click(object sender, RoutedEventArgs e) => Respond(AiUserAction.DismissedPolitely);

        private void Ignore_Click(object sender, RoutedEventArgs e) => Respond(AiUserAction.Ignored);

        private void Respond(AiUserAction action)
        {
            _timeoutTimer.Stop();
            ReportAction(action, closeWindow: true);
        }

        private void ReportAction(AiUserAction action, bool closeWindow)
        {
            if (_actionReported)
                return;

            _actionReported = true;

            // Fire-and-forget: the popup should close immediately rather than wait
            // on a network round trip. SendFeedbackAsync handles its own failures.
            _ = SendFeedbackAsync(action);
            UserActionSelected?.Invoke(this, action);

            if (closeWindow)
                Close();
        }

        /// <summary>Replaces the content when a newer intervention arrives.</summary>
        public void UpdateContent(ActivityResponse response)
        {
            if (response is null)
                return;

            // The previous suggestion went unanswered; record that before replacing it.
            if (!_actionReported)
            {
                _actionReported = true;
                _ = SendFeedbackAsync(AiUserAction.Ignored);
            }

            _activityResponse = response;
            _actionReported = false;
            LoadData(response);

            _timeoutTimer.Stop();
            _timeoutTimer.Start();
        }

        private void LoadData(ActivityResponse response)
        {
            AiMessageTextBlock.Text = response.InterventionMessage ?? "No message available.";

            // Show the risk and the action the agent chose. The old version repeated
            // the message here and labelled the risk score "Detected Activity".
            SuggestionText.Text =
                $"Distraction risk: {response.DistractionRisk:P0}\n" +
                $"Suggested action: {Humanise(response.ActionTaken)}";
        }

        private static string Humanise(string? action) =>
            string.IsNullOrWhiteSpace(action) ? "None" : action.Replace('_', ' ');

        private async Task SendFeedbackAsync(AiUserAction action)
        {
            var interventionId = _activityResponse.InterventionId;
            if (string.IsNullOrEmpty(interventionId))
                return;

            try
            {
                await _feedbackService.SendFeedbackAsync(new FeedbackRequest
                {
                    InterventionId = interventionId,
                    Action = action.ToString(),
                    Helpful = action is AiUserAction.ActedImmediately or AiUserAction.ActedLater,
                    ProductivityChange = 0,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send feedback: {ex.Message}");
            }
        }
    }
}
