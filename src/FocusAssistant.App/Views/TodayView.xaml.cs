using FocusAssistant.ViewModels;
using System;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>Today: how the day is going right now.</summary>
    public partial class TodayView : Page
    {
        private readonly TodayViewModel _viewModel;

        public TodayView(TodayViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            Loaded += async (_, _) => await _viewModel.LoadAsync();
        }
    }
}
