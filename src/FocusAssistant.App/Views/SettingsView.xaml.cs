using FocusAssistant.ViewModels;
using System;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    /// <summary>Settings: the on-device model, and what the app keeps.</summary>
    public partial class SettingsView : Page
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsView(SettingsViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            // Refreshed on show rather than in the constructor: the download state can change
            // while the user is on another screen, and this reads the disk, which a
            // constructor should not.
            Loaded += (_, _) => _viewModel.Refresh();
        }
    }
}
