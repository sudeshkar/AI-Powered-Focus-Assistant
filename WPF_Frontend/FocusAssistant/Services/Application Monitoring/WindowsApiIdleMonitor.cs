    using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Application_Monitoring
{
    public class WindowsApiIdleMonitor : IIdleMonitor, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern uint GetTickCount();

        private readonly TimeSpan _idleThreshold;
        private Timer _monitoringTimer;
        private bool _wasIdle = false;
        private bool _isMonitoring = false;
        public WindowsApiIdleMonitor(TimeSpan? idleThreshold = null)
        {
            _idleThreshold = idleThreshold ?? TimeSpan.FromMinutes(2); // Default 2 minutes
        }
        public bool IsIdle => CurrentIdleTime >= _idleThreshold;

        public TimeSpan CurrentIdleTime {

            get
            {
                LASTINPUTINFO lastInput = new LASTINPUTINFO();
                lastInput.cbSize = (uint)LASTINPUTINFO.SizeOf;

                if (GetLastInputInfo(ref lastInput))
                {
                    uint idleTime = GetTickCount() - lastInput.dwTime;
                    return TimeSpan.FromMilliseconds(idleTime);
                }
                return TimeSpan.Zero;
            }

        }

        public bool IsMonitoring => _isMonitoring;

        public event EventHandler<IdleStateChangedEventArgs> IdleStateChanged;

        public void Dispose()
        {
            StopMonitoring();
            Console.WriteLine($"IdleMonitor disposed at {DateTime.Now:HH:mm:ss.fff}");
        }

        public void StartMonitoring()
        {
            if (_isMonitoring)
            {
                Console.WriteLine("IdleMonitor already monitoring, skipping start.");
                return;
            }
            _isMonitoring = true;
            _wasIdle = IsIdle;
            _monitoringTimer = new Timer(CheckIdleState, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            Console.WriteLine($"IdleMonitor started at {DateTime.Now:HH:mm:ss.fff}");
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring)
            {
                Console.WriteLine("IdleMonitor not monitoring, skipping stop.");
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
                    Console.WriteLine($"IdleMonitor stopped at {DateTime.Now:HH:mm:ss.fff}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping IdleMonitor: {ex.Message}");
            }
        }
        private void CheckIdleState(object state)
        {
            try
            {
                bool currentlyIdle = IsIdle;

                if (_wasIdle != currentlyIdle)
                {
                    _wasIdle = currentlyIdle;

                    IdleStateChanged?.Invoke(this, new IdleStateChangedEventArgs
                    {
                        IsIdle = currentlyIdle,
                        IdleTime = CurrentIdleTime,
                        ChangeTime = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking idle state: {ex.Message}");
            }
        }
    }
}
