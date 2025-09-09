using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Models.Events;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Application_Monitoring
{
    public class WindowsApiWindowMonitor : IWindowMonitor, IDisposable
    {
        // Windows API imports
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private Timer _monitoringTimer;
        private string _lastAppName;
        private string _lastWindowTitle;
        private bool _isMonitoring = false;

        public bool IsMonitoring => _isMonitoring;

        public TimeSpan PollingInterval { get; }

        public event EventHandler<AppWindowChangedEventArgs> WindowChanged;

        public WindowsApiWindowMonitor(TimeSpan? pollingInterval = null)
        {
            PollingInterval = pollingInterval ?? TimeSpan.FromSeconds(1);
            Console.WriteLine($"Initialized with PollingInterval: {PollingInterval.TotalSeconds} seconds");
        }

        public (string appName, string windowTitle) GetActiveWindow()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                if (handle == IntPtr.Zero)
                    return (null, null);

                // Get window title
                int length = GetWindowTextLength(handle);
                if (length == 0)
                    return (null, null);

                StringBuilder windowTitle = new StringBuilder(length + 1);
                GetWindowText(handle, windowTitle, windowTitle.Capacity);

                // Get process name
                GetWindowThreadProcessId(handle, out uint processId);
                using (Process process = Process.GetProcessById((int)processId))
                {
                    return (process.ProcessName, windowTitle.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting active window: {ex.Message}");
                return (null, null);
            }
        }

        public void StartMonitoring()
        {
            if (_isMonitoring)
            {
                Console.WriteLine("WindowMonitor already monitoring, skipping start.");
                return;
            }
            _isMonitoring = true;
            var (appName, windowTitle) = GetActiveWindow();
            _lastAppName = appName;
            _lastWindowTitle = windowTitle;
            _monitoringTimer = new Timer(CheckWindowChange, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            Console.WriteLine($"WindowMonitor started at {DateTime.Now:HH:mm:ss.fff}");
        }


        public void StopMonitoring()
        {
            if (!_isMonitoring)
            {
                Console.WriteLine("WindowMonitor not monitoring, skipping stop.");
                return;
            }
            _isMonitoring = false;
            try
            {
                if (_monitoringTimer != null)
                {
                    _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _monitoringTimer.Dispose();
                    _monitoringTimer = null;
                    Console.WriteLine($"WindowMonitor stopped at {DateTime.Now:HH:mm:ss.fff}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping WindowMonitor: {ex.Message}");
            }
        }
        private void CheckWindowChange(object state)
        {
            try
            {
                var (currentApp, currentTitle) = GetActiveWindow();

                if (string.IsNullOrEmpty(currentApp)) return;

                // Check if window changed
                if (_lastAppName != currentApp || _lastWindowTitle != currentTitle)
                {
                    var eventArgs = new AppWindowChangedEventArgs
                    {
                        PreviousAppName = _lastAppName,
                        PreviousWindowTitle = _lastWindowTitle,
                        CurrentAppName = currentApp,
                        CurrentWindowTitle = currentTitle,
                        ChangeTime = DateTime.Now
                    };

                    _lastAppName = currentApp;
                    _lastWindowTitle = currentTitle;

                    WindowChanged?.Invoke(this, eventArgs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in window change detection: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopMonitoring();
            Console.WriteLine($"WindowMonitor disposed at {DateTime.Now:HH:mm:ss.fff}");
        }
    }
}
