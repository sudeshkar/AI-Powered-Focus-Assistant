using FocusAssistant.Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Datafetch;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.ViewModels;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    public partial class TrackingView : UserControl
    {

        public TrackingView(TrackingViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;


        }
    }
}
