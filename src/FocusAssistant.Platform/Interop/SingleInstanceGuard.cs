using System;
using System.Threading;

namespace FocusAssistant.Platform.Interop
{
    /// <summary>
    /// Ensures only one copy of the app runs, and brings the existing one forward.
    /// </summary>
    /// <remarks>
    /// Two instances would mean two window monitors, two overlapping sessions, and two
    /// writers on one SQLite file. That was hard to reach while the app was a window you
    /// launched deliberately; with a tray icon and a startup entry it becomes ordinary -
    /// the user clicks the shortcut again because the window is hidden.
    /// <para>
    /// A named mutex rather than a process scan: process names are not unique and scanning
    /// races another copy doing the same scan. The mutex is per-user, not global, so two
    /// people on one machine each get their own instance and their own database.
    /// </para>
    /// </remarks>
    public sealed class SingleInstanceGuard : IDisposable
    {
        private const string MutexName = @"Local\FocusAssistant.SingleInstance";
        private const string ActivateEventName = @"Local\FocusAssistant.Activate";

        private readonly Mutex _mutex;
        private readonly EventWaitHandle _activateSignal;
        private RegisteredWaitHandle? _registration;
        private bool _disposed;

        private SingleInstanceGuard(Mutex mutex, EventWaitHandle activateSignal)
        {
            _mutex = mutex;
            _activateSignal = activateSignal;
        }

        /// <summary>
        /// Takes ownership if no other instance holds it.
        /// </summary>
        /// <returns>
        /// The guard when this process is the only instance, or null when another already
        /// runs - in which case that one has been signalled to show itself and this process
        /// should exit quietly.
        /// </returns>
        public static SingleInstanceGuard? TryAcquire()
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out var isOwner);
            var signal = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);

            if (!isOwner)
            {
                // Ask the running instance to surface, then get out of its way. Without
                // this, clicking the shortcut while the window is hidden in the tray looks
                // like the app failed to start.
                signal.Set();
                mutex.Dispose();
                signal.Dispose();
                return null;
            }

            return new SingleInstanceGuard(mutex, signal);
        }

        /// <summary>
        /// Invokes <paramref name="onActivate"/> whenever another copy is launched.
        /// </summary>
        public void OnSecondInstanceLaunched(Action onActivate)
        {
            ArgumentNullException.ThrowIfNull(onActivate);

            // A registered wait rather than a dedicated thread: this is idle almost always,
            // and the thread pool handles the handful of times it fires.
            _registration = ThreadPool.RegisterWaitForSingleObject(
                _activateSignal,
                (_, _) => onActivate(),
                state: null,
                timeout: System.Threading.Timeout.InfiniteTimeSpan,
                executeOnlyOnce: false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _registration?.Unregister(null);

            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not the owning thread, or already released. Either way the handle below
                // is what actually frees it.
            }

            _mutex.Dispose();
            _activateSignal.Dispose();
        }
    }
}
