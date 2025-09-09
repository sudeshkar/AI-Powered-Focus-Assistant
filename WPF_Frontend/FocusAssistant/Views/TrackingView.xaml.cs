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

            Loaded += async (s, e) =>
            {
                await viewModel.LoadAnalyticsAsync();
            };
        }

        // Optional: Toast notifications logic stays here
    }
}
