using FocusAssistant.ViewModels;
using System;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>Daily analytics read from the local database.</summary>
    public partial class AnalyticsView : UserControl
    {
        // The view model is injected rather than built here from a resolved
        // IServiceProvider; the previous version new'd up both the service and the
        // view model by hand, duplicating registrations the container already had.
        public AnalyticsView(AnalyticsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
    }
}
