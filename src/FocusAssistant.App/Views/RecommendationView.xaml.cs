using FocusAssistant.ViewModels;
using System;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>What the agent has learned, and its most recent suggestion.</summary>
    public partial class RecommendationsView : Page
    {
        private readonly RecommendationViewModel _viewModel;

        public RecommendationsView(RecommendationViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                await _viewModel.LoadAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not load recommendations: {ex.Message}");
            }
        }
    }
}
