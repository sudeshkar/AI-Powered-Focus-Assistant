using FocusAssistant.Core.Intervention;
using FocusAssistant.Core.Monitoring;
using FocusAssistant.Views;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FocusAssistant.Intervention
{
    /// <summary>
    /// Shows a nudge window. The one piece of the intervention pipeline that has to run on
    /// the UI thread, since everything upstream of it runs on a background timer.
    /// </summary>
    public sealed class NudgeDispatcher : IInterventionDispatcher
    {
        private readonly IWindowActivator _windowActivator;

        public NudgeDispatcher(IWindowActivator windowActivator)
        {
            _windowActivator = windowActivator ?? throw new ArgumentNullException(nameof(windowActivator));
        }

        public Task<InterventionResponse> ShowAsync(InterventionSuggestion suggestion, CancellationToken ct = default)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
                return Task.FromResult(InterventionResponse.Ignored);

            return dispatcher.InvokeAsync(async () =>
            {
                var window = new NudgeWindow(suggestion, _windowActivator);
                return await window.ShowAndAwaitResponseAsync();
            }).Task.Unwrap();
        }
    }
}
