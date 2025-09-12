using FocusAssistant.Enums;
using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Datafetch;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.ViewModels;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    public partial class TrackingView : UserControl
    {
        private readonly TrackingViewModel _viewModel;
        private AiInterventionWindow _activeAiPopup;
        private readonly IFeedbackService _feedbackService;  // FIX: Now properly assigned via constructor.

        // FIX: Static lock for thread-safe singleton enforcement (prevents races on rapid events).
        private static readonly object _popupLock = new object();

        public TrackingView(TrackingViewModel viewModel, IFeedbackService feedbackService)  // FIX: Add feedbackService param.
        {
            InitializeComponent();

            _viewModel = viewModel;
            _feedbackService = feedbackService;  // FIX: Assign it.
            DataContext = _viewModel;

            _viewModel.AiInterventionOccurred += ViewModel_AiInterventionOccurred;
        }

        // FIX: Enhanced with lock, early validation (redundant but safe), and better lifecycle.
        // Now the single source of truth for popups.
        private void ViewModel_AiInterventionOccurred(object? sender, ActivityResponse e)
        {
            // Early skip (already done in ViewModel, but double-check).
            if (e == null || e.InterventionId == null || string.IsNullOrEmpty(e.InterventionMessage))
            {
                Console.WriteLine("Skipping popup: Invalid intervention response");
                return;
            }

            Dispatcher.Invoke(() =>
            {
                lock (_popupLock)  // FIX: Atomic lock to prevent race conditions.
                {
                    if (_activeAiPopup != null && _activeAiPopup.IsVisible)
                    {
                        Console.WriteLine("Popup already open, updating data...");
                        _activeAiPopup.UpdateContent(e);
                        return;
                    }

                    // Create new popup
                    _activeAiPopup = new AiInterventionWindow(e, _feedbackService);

                    // Subscribe to user action (centralized handling).
                    _activeAiPopup.UserActionSelected += (s, action) => HandleUserAction(e, action);

                    // FIX: Handle close to clear reference (use Deactivated for non-modal hide if needed).
                    _activeAiPopup.Closed += (s, args) => _activeAiPopup = null;

                    // Show dialog
                    _activeAiPopup.Show();
                }
            });
        }

        // FIX: Forward to ViewModel for consistency; complete logging.
        private void HandleUserAction(ActivityResponse e, AiUserAction action)
        {
            _viewModel.HandleUserAction(e, action);  // Delegate to ViewModel.
            Console.WriteLine("User action handled in UI layer.");
        }
    }
}