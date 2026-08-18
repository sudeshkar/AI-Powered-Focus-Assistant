using FocusAssistant.ViewModels;
using System;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>What the agent has learned, and its most recent suggestion.</summary>
    public partial class RecommendationsView : UserControl
    {
        public RecommendationsView(RecommendationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
    }
}
