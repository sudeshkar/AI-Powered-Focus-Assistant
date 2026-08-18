using FocusAssistant.Enums;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>Live tracking status, and host for the intervention popup.</summary>
    public partial class TrackingView : UserControl
    {
        private readonly TrackingViewModel _viewModel;
        private readonly IFeedbackService _feedbackService;

        private AiInterventionWindow? _activePopup;

        public TrackingView(TrackingViewModel viewModel, IFeedbackService feedbackService)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));

            DataContext = _viewModel;
            _viewModel.AiInterventionOccurred += OnAiInterventionOccurred;
        }

        private void OnAiInterventionOccurred(object? sender, ActivityResponse e)
        {
            if (e?.InterventionId is null || string.IsNullOrEmpty(e.InterventionMessage))
                return;

            // Marshal to the UI thread, then run entirely on it. All popup state is
            // touched only here, so the dispatcher is the synchronisation and the
            // previous static lock is unnecessary.
            Dispatcher.InvokeAsync(() => ShowIntervention(e));
        }

        private void ShowIntervention(ActivityResponse response)
        {
            // At most one popup at a time: a second would stack on top of the first.
            if (_activePopup is { IsVisible: true })
            {
                _activePopup.UpdateContent(response);
                return;
            }

            var popup = new AiInterventionWindow(response, _feedbackService)
            {
                Owner = Window.GetWindow(this),
            };

            popup.UserActionSelected += (_, action) => _viewModel.HandleUserAction(response, action);
            popup.Closed += (_, _) => _activePopup = null;

            _activePopup = popup;
            popup.Show();
        }
    }
}
