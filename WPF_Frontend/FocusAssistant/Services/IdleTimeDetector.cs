using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public class IdleTimeDetector
    {
        // Windows API for getting last input time
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));
            [MarshalAs(UnmanagedType.U4)]
            public uint cbSize;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwTime;
        }

        private Timer _idleTimer;
        private bool _isMonitoring = false;
        private DateTime _lastActivityTime;
        private bool _isCurrentlyIdle = false;
        private readonly int _idleThresholdSeconds;

        public event EventHandler<IdleStateChangedEventArgs> IdleStateChanged;
        public event EventHandler<TimeSpan> IdleTimeUpdated;

        public bool IsIdle => _isCurrentlyIdle;
        public TimeSpan CurrentIdleTime => GetIdleTime();
        public bool IsMonitoring => _isMonitoring;

        public IdleTimeDetector(int idleThresholdSeconds = 300) // 5 minutes default
        {
            _idleThresholdSeconds = idleThresholdSeconds;
            _lastActivityTime = DateTime.Now;
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _lastActivityTime = DateTime.Now;
            _isCurrentlyIdle = false;

            // Check idle state every 10 seconds
            _idleTimer = new Timer(CheckIdleState, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

            Console.WriteLine($"🕐 Idle time monitoring started (threshold: {_idleThresholdSeconds}s)");
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _idleTimer?.Dispose();

            Console.WriteLine("⏹️ Idle time monitoring stopped");
        }

        private void CheckIdleState(object state)
        {
            try
            {
                var idleTime = GetIdleTime();
                var wasIdle = _isCurrentlyIdle;
                var isNowIdle = idleTime.TotalSeconds >= _idleThresholdSeconds;

                // Update idle state if changed
                if (wasIdle != isNowIdle)
                {
                    _isCurrentlyIdle = isNowIdle;
                    var eventArgs = new IdleStateChangedEventArgs
                    {
                        IsIdle = isNowIdle,
                        IdleDuration = idleTime,
                        StateChangeTime = DateTime.Now
                    };

                    IdleStateChanged?.Invoke(this, eventArgs);

                    Console.WriteLine($"🔄 Idle state changed: {(isNowIdle ? "IDLE" : "ACTIVE")} (duration: {idleTime:mm\\:ss})");
                }

                // Always fire the idle time update event
                IdleTimeUpdated?.Invoke(this, idleTime);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking idle state: {ex.Message}");
            }
        }

        private TimeSpan GetIdleTime()
        {
            try
            {
                var lastInput = new LASTINPUTINFO();
                lastInput.cbSize = (uint)LASTINPUTINFO.SizeOf;

                if (GetLastInputInfo(ref lastInput))
                {
                    var tickCount = GetTickCount();
                    var idleMilliseconds = tickCount - lastInput.dwTime;
                    return TimeSpan.FromMilliseconds(idleMilliseconds);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting idle time: {ex.Message}");
            }

            return TimeSpan.Zero;
        }

        public void Dispose()
        {
            StopMonitoring();
            _idleTimer?.Dispose();
        }
    }

    public class IdleStateChangedEventArgs : EventArgs
    {
        public bool IsIdle { get; set; }
        public TimeSpan IdleDuration { get; set; }
        public DateTime StateChangeTime { get; set; }
    }
}
