using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FocusAssistant.ViewModels
{
    /// <summary>
    /// ICommand over an async handler, disabled while it runs so a slow operation
    /// cannot be started twice.
    /// </summary>
    /// <remarks>
    /// Replaces the duplicate RelayCommand that used to be declared in the global
    /// namespace inside RecommendationViewModel.cs.
    /// </remarks>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public event EventHandler? CanExecuteChanged;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute();
            }
            catch (Exception ex)
            {
                // async void: an escaping exception would terminate the process.
                Console.WriteLine($"Command failed: {ex.Message}");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
    }
}
