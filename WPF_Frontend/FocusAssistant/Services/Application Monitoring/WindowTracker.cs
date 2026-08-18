using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Application_Monitoring
{
    /// <summary>
    /// Starts and stops tracking as one unit: the session, the window monitor and
    /// the idle monitor come up and go down together.
    /// </summary>
    /// <remarks>
    /// The monitors raise events that <see cref="Session.SessionManager"/> subscribes
    /// to directly; this type only owns the on/off transition.
    /// </remarks>
    public class WindowTracker
    {
        private readonly IWindowMonitor _windowMonitor;
        private readonly IIdleMonitor _idleMonitor;
        private readonly ISessionManager _sessionManager;

        // Start and stop can both be triggered from the UI; without this a
        // double-click could interleave them.
        private readonly SemaphoreSlim _transitionLock = new(1, 1);

        private volatile bool _isTracking;

        public bool IsTracking => _isTracking;

        public WindowTracker(
            IWindowMonitor windowMonitor,
            IIdleMonitor idleMonitor,
            ISessionManager sessionManager)
        {
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _idleMonitor = idleMonitor ?? throw new ArgumentNullException(nameof(idleMonitor));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public async Task StartTrackingAsync()
        {
            await _transitionLock.WaitAsync();
            try
            {
                if (_isTracking)
                    return;

                // Session first: the monitors start raising events immediately, and
                // there must be a session for those events to accumulate into.
                await _sessionManager.StartSessionAsync();

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

                await _sessionManager.EndSessionAsync();
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
                await _sessionManager.EndSessionAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup after failed start also failed: {ex.Message}");
            }
        }
    }
}
