using FocusAssistant.Core.Monitoring;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FocusAssistant.Platform.Monitoring
{
    /// <summary>
    /// Finds a running process's main window and brings it forward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Windows restricts <c>SetForegroundWindow</c>: a background process generally cannot
    /// steal focus from whatever the user is doing, by design, and this app's nudge click
    /// handler runs on a window that deliberately never took focus itself
    /// (<c>WS_EX_NOACTIVATE</c>), so it does not automatically inherit the standing to grant
    /// it either. <c>AttachThreadInput</c> is the documented way around that: temporarily
    /// joining input queues with the current foreground thread borrows its permission for
    /// the duration of the call, then detaches immediately after.
    /// </para>
    /// <para>
    /// A process can have several top-level windows (a browser with multiple windows, for
    /// instance); this activates <see cref="Process.MainWindowHandle"/>; which one that is,
    /// is decided by Windows, not by this class.
    /// </para>
    /// </remarks>
    public sealed class WindowsApiWindowActivator : IWindowActivator
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        private readonly ILogger<WindowsApiWindowActivator> _logger;

        public WindowsApiWindowActivator(ILogger<WindowsApiWindowActivator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool ActivateByProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            try
            {
                var candidates = Process.GetProcessesByName(processName);
                var target = Array.Find(candidates, p => p.MainWindowHandle != IntPtr.Zero);

                if (target is null)
                {
                    _logger.LogDebug("No window found for process {Process}", processName);
                    return false;
                }

                var handle = target.MainWindowHandle;

                if (IsIconic(handle))
                    ShowWindow(handle, SW_RESTORE);

                var foreground = GetForegroundWindow();
                GetWindowThreadProcessId(foreground, out var foregroundThread);
                var currentThread = GetCurrentThreadId();

                // Attach-set-detach: borrow the foreground thread's standing to change
                // focus just long enough to make the call, then give it back immediately.
                var attached = foregroundThread != currentThread
                    && AttachThreadInput(currentThread, foregroundThread, true);

                try
                {
                    return SetForegroundWindow(handle);
                }
                finally
                {
                    if (attached)
                        AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not activate {Process}", processName);
                return false;
            }
        }
    }
}
