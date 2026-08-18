using FocusAssistant.Core.Session;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Monitoring
{
    /// <summary>
    /// Starts and stops tracking as one unit: the session, the window monitor and
    /// the idle monitor come up and go down together.
    /// </summary>
    /// <remarks>
    /// The monitors raise events that <see cref="SessionEngine"/> subscribes to
    /// directly; this type only owns the on/off transition.
    /// </remarks>
    public class WindowTracker
    {
        private readonly IWindowMonitor _windowMonitor;
        private readonly IIdleMonitor _idleMonitor;
        private readonly ISessionEngine _sessionEngine;

        // Start and stop can both be triggered from the UI; without this a
        // double-click could interleave them.
        private readonly SemaphoreSlim _transitionLock = new(1, 1);

        private volatile bool _isTracking;

        public bool IsTracking => _isTracking;

        public WindowTracker(
            IWindowMonitor windowMonitor,
            IIdleMonitor idleMonitor,
            ISessionEngine sessionEngine)
        {
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _idleMonitor = idleMonitor ?? throw new ArgumentNullException(nameof(idleMonitor));
            _sessionEngine = sessionEngine ?? throw new ArgumentNullException(nameof(sessionEngine));
        }

        public async Task StartTrackingAsync(string? goal = null)
        {
            await _transitionLock.WaitAsync();
            try
            {
                if (_isTracking)
                    return;

                // Session first: the monitors start raising events immediately, and
                // there must be a session for those events to accumulate into.
                await _sessionEngine.StartSessionAsync(goal);

                _windowMonitor.StartMonitoring();
                _idleMonitor.StartMonitoring();
                _isTracking = true;

                Console.WriteLine("Tracking started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start tracking: {ex.Message}");
                await SafeStopAsync();
                throw;
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        public async Task StopTrackingAsync()
        {
            await _transitionLock.WaitAsync();
            try
            {
                if (!_isTracking)
                    return;

                // Monitors first, so no further events arrive while the session is
                // being finalised and written out.
                _windowMonitor.StopMonitoring();
                _idleMonitor.StopMonitoring();
                _isTracking = false;

                await _sessionEngine.EndSessionAsync();
                Console.WriteLine("Tracking stopped.");
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        /// <summary>Best-effort teardown used when startup fails partway through.</summary>
        private async Task SafeStopAsync()
        {
            _isTracking = false;
            try
            {
                _windowMonitor.StopMonitoring();
                _idleMonitor.StopMonitoring();
                await _sessionEngine.EndSessionAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup after failed start also failed: {ex.Message}");
            }
        }
    }
}
