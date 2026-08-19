using FocusAssistant.Core.Monitoring;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace FocusAssistant.Platform.Monitoring
{
    /// <summary>
    /// Reports when the user goes idle, based on time since the last keyboard or
    /// mouse input.
    /// </summary>
    public class WindowsApiIdleMonitor : IIdleMonitor, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        // GetTickCount64 rather than GetTickCount: the 32-bit counter wraps after
        // ~49.7 days of uptime, which made idle time jump to enormous values.
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        private static readonly int LastInputInfoSize = Marshal.SizeOf<LASTINPUTINFO>();

        private readonly TimeSpan _idleThreshold;
        private readonly TimeSpan _pollInterval;
        private readonly object _gate = new();

        private Timer? _monitoringTimer;
        private bool _wasIdle;
        private bool _isMonitoring;

        private readonly ILogger<WindowsApiIdleMonitor> _logger;

        public WindowsApiIdleMonitor(
            ILogger<WindowsApiIdleMonitor> logger,
            TimeSpan? idleThreshold = null,
            TimeSpan? pollInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idleThreshold = idleThreshold ?? TimeSpan.FromMinutes(2);
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        }

        public bool IsIdle => CurrentIdleTime >= _idleThreshold;

        public TimeSpan CurrentIdleTime
        {
            get
            {
                var lastInput = new LASTINPUTINFO { cbSize = (uint)LastInputInfoSize };
                if (!GetLastInputInfo(ref lastInput))
                    return TimeSpan.Zero;

                // dwTime is a 32-bit tick value; compare it in the same 32-bit space
                // as the low half of the 64-bit counter so subtraction stays correct.
                var now = unchecked((uint)GetTickCount64());
                var elapsed = unchecked(now - lastInput.dwTime);
                return TimeSpan.FromMilliseconds(elapsed);
            }
        }

        public bool IsMonitoring => _isMonitoring;

        public event EventHandler<IdleStateChangedEventArgs>? IdleStateChanged;

        public void StartMonitoring()
        {
            lock (_gate)
            {
                if (_isMonitoring)
                    return;

                _isMonitoring = true;
                _wasIdle = IsIdle;
                _monitoringTimer = new Timer(CheckIdleState, null, _pollInterval, _pollInterval);
                _logger.LogInformation("Idle monitor started (threshold {Minutes:0.#} min)", _idleThreshold.TotalMinutes);
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
                _logger.LogInformation("Idle monitor stopped");
            }
        }

        private void CheckIdleState(object? state)
        {
            try
            {
                if (!_isMonitoring)
                    return;

                var currentlyIdle = IsIdle;
                if (_wasIdle == currentlyIdle)
                    return;

                _wasIdle = currentlyIdle;
                IdleStateChanged?.Invoke(this, new IdleStateChangedEventArgs
                {
                    IsIdle = currentlyIdle,
                    IdleTime = CurrentIdleTime,
                    ChangeTime = DateTime.Now,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking idle state");
            }
        }

        public void Dispose() => StopMonitoring();
    }
}
