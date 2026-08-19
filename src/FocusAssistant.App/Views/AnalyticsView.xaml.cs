using FocusAssistant.ViewModels;
using System;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>Daily analytics read from the local database.</summary>
    public partial class AnalyticsView : Page
    {
        // The view model is injected rather than built here from a resolved
        // IServiceProvider; the previous version new'd up both the service and the
        // view model by hand, duplicating registrations the container already had.
        private readonly AnalyticsViewModel _viewModel;

        public AnalyticsView(AnalyticsViewModel viewModel)
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
                await _viewModel.LoadDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not load analytics: {ex.Message}");
            }
        }
    }
}
