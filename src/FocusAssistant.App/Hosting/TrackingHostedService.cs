using FocusAssistant.Core.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Owns tracking for the lifetime of the process.
    /// </summary>
    /// <remarks>
    /// Tracking used to start in MainWindow.Loaded and stop in MainWindow.Closed, which tied
    /// it to a window being open - a focus tracker that only works while you are looking at
    /// it, which is the opposite of the point. It belongs to the application, so it lives
    /// here, and closing the window now hides it rather than ending the day's session.
    /// </remarks>
    public sealed class TrackingHostedService : IHostedService
    {
        private readonly WindowTracker _windowTracker;
        private readonly StartupState _startupState;
        private readonly ILogger<TrackingHostedService> _logger;

        public TrackingHostedService(
            WindowTracker windowTracker,
            StartupState startupState,
            ILogger<TrackingHostedService> logger)
        {
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(StartTrackingAsync, CancellationToken.None);
            return Task.CompletedTask;
        }

        private async Task StartTrackingAsync()
        {
            try
            {
                // The schema may not exist yet: migrations run on their own thread so the
                // window can paint immediately.
                if (!await _startupState.DatabaseReady.ConfigureAwait(false))
                {
                    _logger.LogWarning("Not starting tracking - the database is unavailable");
                    return;
                }

                if (!_windowTracker.IsTracking)
                    await _windowTracker.StartTrackingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not start tracking");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_windowTracker.IsTracking)
                    await _windowTracker.StopTrackingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not stop tracking cleanly");
            }
        }
    }
}
