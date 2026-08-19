using FocusAssistant.ViewModels;
using System;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>Insights: patterns across a week or a month, not just today.</summary>
    public partial class InsightsView : Page
    {
        private readonly InsightsViewModel _viewModel;

        public InsightsView(InsightsViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            Loaded += async (_, _) => await _viewModel.LoadAsync();
        }
    }
}
