using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Datafetch;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.ViewModels;
using MailChimp.Net.Models;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    public partial class TrackingView : UserControl
    {

        private readonly TrackingViewModel _viewModel;

        public TrackingView(TrackingViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.AiInterventionOccurred += ViewModel_AiInterventionOccurred;

        }

        private void ViewModel_AiInterventionOccurred(object? sender, ActivityResponse e)
        {
            Dispatcher.Invoke(() =>
            {
                var aiWindow = new AiInterventionWindow(e);
                aiWindow.Show();

                // Optional: log in a ListBox/TextBlock in TrackingView
                
            });
        }
    }
}
