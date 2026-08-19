using FocusAssistant.ViewModels;
using System;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>Live tracking status: current app, activity log, and today's totals.</summary>
    public partial class TrackingView : Page
    {
        public TrackingView(TrackingViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
    }
}
