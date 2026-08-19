using CommunityToolkit.Mvvm.ComponentModel;
using FocusAssistant.Core.Monitoring;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Privacy
{
    /// <summary>
    /// Pauses tracking for a fixed duration or until tomorrow.
    /// </summary>
    /// <remarks>
    /// Pausing stops the window and idle monitors outright, through the same
    /// <see cref="WindowTracker.StopTrackingAsync"/> a manual stop would use - it does not
    /// merely skip writing rows while quietly continuing to read window titles. "Pause"
    /// has to mean the app stops looking, not just stops remembering what it saw; anything
    /// less would not be pausing, it would be a promise about storage that the reading side
    /// never agreed to.
    /// </remarks>
    public sealed partial class PauseController : ObservableObject
    {
        private readonly WindowTracker _windowTracker;
        private readonly ILogger<PauseController> _logger;

        private readonly object _gate = new();
        private Timer? _resumeTimer;

        [ObservableProperty]
        private bool _isPaused;

        [ObservableProperty]
        private DateTimeOffset? _resumesAt;

        public PauseController(WindowTracker windowTracker, ILogger<PauseController> logger)
        {
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task PauseAsync(TimeSpan duration) => PauseUntilAsync(DateTimeOffset.Now + duration);

        /// <summary>Pauses until the next local midnight.</summary>
        public Task PauseUntilTomorrowAsync()
        {
            var tomorrow = DateTimeOffset.Now.Date.AddDays(1);
            return PauseUntilAsync(tomorrow);
        }

        private async Task PauseUntilAsync(DateTimeOffset until)
        {
            var delay = until - DateTimeOffset.Now;
            if (delay <= TimeSpan.Zero)
                return;

            lock (_gate)
            {
                _resumeTimer?.Dispose();
                _resumeTimer = new Timer(_ => _ = ResumeAsync(), null, delay, Timeout.InfiniteTimeSpan);
            }

            if (_windowTracker.IsTracking)
                await _windowTracker.StopTrackingAsync().ConfigureAwait(false);

            IsPaused = true;
            ResumesAt = until;
            _logger.LogInformation("Tracking paused until {ResumesAt}", until);
        }

        public async Task ResumeAsync()
        {
            lock (_gate)
            {
                _resumeTimer?.Dispose();
                _resumeTimer = null;
            }

            if (!_windowTracker.IsTracking)
                await _windowTracker.StartTrackingAsync().ConfigureAwait(false);

            IsPaused = false;
            ResumesAt = null;
            _logger.LogInformation("Tracking resumed");
        }
    }
}
