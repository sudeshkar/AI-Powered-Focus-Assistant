using FocusAssistant.Core.Monitoring;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FocusAssistant.Platform.Monitoring
{
    /// <summary>
    /// Polls the foreground window and raises <see cref="WindowChanged"/> when the
    /// user moves to a different application.
    /// </summary>
    public class WindowsApiWindowMonitor : IWindowMonitor, IDisposable
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private readonly object _gate = new();
        private Timer? _monitoringTimer;
        private string? _lastAppName;
        private string? _lastWindowTitle;
        private bool _isMonitoring;

        // Guards against overlapping callbacks: the poll does a cross-process
        // lookup, which can outrun a short interval.
        private int _pollInFlight;

        public bool IsMonitoring => _isMonitoring;

        public TimeSpan PollingInterval { get; }

        public event EventHandler<AppWindowChangedEventArgs>? WindowChanged;

        private readonly ILogger<WindowsApiWindowMonitor> _logger;

        public WindowsApiWindowMonitor(
            ILogger<WindowsApiWindowMonitor> logger,
            TimeSpan? pollingInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            PollingInterval = pollingInterval ?? TimeSpan.FromSeconds(1);
        }

        public (string? appName, string? windowTitle) GetActiveWindow()
        {
            try
            {
                var handle = GetForegroundWindow();
                if (handle == IntPtr.Zero)
                    return (null, null);

                var length = GetWindowTextLength(handle);
                var title = new StringBuilder(length + 1);
                if (length > 0)
                    GetWindowText(handle, title, title.Capacity);

                GetWindowThreadProcessId(handle, out var processId);
                if (processId == 0)
                    return (null, null);

                using var process = Process.GetProcessById((int)processId);
                return (process.ProcessName, title.ToString());
            }
            catch (ArgumentException)
            {
                // The process exited between the handle lookup and GetProcessById.
                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading active window");
                return (null, null);
            }
        }

        public void StartMonitoring()
        {
            lock (_gate)
            {
                if (_isMonitoring)
                    return;

                _isMonitoring = true;
                (_lastAppName, _lastWindowTitle) = GetActiveWindow();

                // Honour the configured interval. This used to hard-code one second,
                // making PollingInterval decorative.
                _monitoringTimer = new Timer(CheckWindowChange, null, PollingInterval, PollingInterval);
                _logger.LogInformation("Window monitor started (every {Seconds:0.#}s)", PollingInterval.TotalSeconds);
            }
        }

        public void StopMonitoring()
        {
            lock (_gate)
            {
                if (!_isMonitoring)
                    return;

                _isMonitoring = false;
                _monitoringTimer?.Dispose();
                _monitoringTimer = null;
                _logger.LogInformation("Window monitor stopped");
            }
        }

        private void CheckWindowChange(object? state)
        {
            // Drop this tick if the previous one is still running.
            if (Interlocked.Exchange(ref _pollInFlight, 1) == 1)
                return;

            try
            {
                if (!_isMonitoring)
                    return;

                var (currentApp, currentTitle) = GetActiveWindow();
                if (string.IsNullOrEmpty(currentApp))
                    return;

                // Only an application switch counts. Firing on title changes too
                // meant every keystroke that updated a document title created an
                // AppUsage row and a backend round-trip.
                if (string.Equals(_lastAppName, currentApp, StringComparison.OrdinalIgnoreCase))
                {
                    _lastWindowTitle = currentTitle;
                    return;
                }

                var args = new AppWindowChangedEventArgs
                {
                    PreviousAppName = _lastAppName,
                    PreviousWindowTitle = _lastWindowTitle,
                    CurrentAppName = currentApp,
                    CurrentWindowTitle = currentTitle,
                    ChangeTime = DateTime.Now,
                };

                _lastAppName = currentApp;
                _lastWindowTitle = currentTitle;

                WindowChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                // A throw here would take down the timer thread.
                _logger.LogWarning(ex, "Error in window change detection");
            }
            finally
            {
                Interlocked.Exchange(ref _pollInFlight, 0);
            }
        }

        public void Dispose() => StopMonitoring();
    }
}
