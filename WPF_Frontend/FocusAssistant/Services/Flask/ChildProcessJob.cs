using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FocusAssistant.Services.Flask
{
    /// <summary>
    /// A Windows job object that kills its members when the handle closes.
    /// </summary>
    /// <remarks>
    /// Assigning the backend process to this job makes Windows terminate it when
    /// this application exits, for any reason — a clean shutdown, an unhandled
    /// exception, or being killed from Task Manager. Relying on a shutdown handler
    /// alone left the Python process running and port 5000 bound whenever that
    /// handler did not get to run.
    /// </remarks>
    public sealed class ChildProcessJob : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        /// <summary>True when the job was created and can accept processes.</summary>
        public bool IsAvailable => _handle != IntPtr.Zero;

        public ChildProcessJob()
        {
            try
            {
                _handle = CreateJobObject(IntPtr.Zero, null);
                if (_handle == IntPtr.Zero)
                    return;

                var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
                    },
                };

                var size = Marshal.SizeOf(limits);
                var pointer = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(limits, pointer, false);
                    if (!SetInformationJobObject(_handle, JobObjectExtendedLimitInformation, pointer, (uint)size))
                    {
                        CloseHandle(_handle);
                        _handle = IntPtr.Zero;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pointer);
                }
            }
            catch (DllNotFoundException)
            {
                // Not on Windows: fall back to explicit process termination.
                _handle = IntPtr.Zero;
            }
        }

        /// <summary>Ties the process's lifetime to this job. Best effort.</summary>
        public bool TryAssign(Process process)
        {
            if (!IsAvailable)
                return false;

            try
            {
                return AssignProcessToJobObject(_handle, process.Handle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not attach backend to the job object: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_handle != IntPtr.Zero)
            {
                // Closing the last handle terminates every process in the job.
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr securityAttributes, string? name);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(
            IntPtr job, int infoClass, IntPtr info, uint infoLength);

        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
