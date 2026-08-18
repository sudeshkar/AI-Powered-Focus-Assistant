using FocusAssistant.Core.Monitoring;
using System;

namespace FocusAssistant.Core.Monitoring
{
    /// <summary>Watches which application currently has focus.</summary>
    public interface IWindowMonitor
    {
        /// <summary>The foreground app and window title, or (null, null) if unavailable.</summary>
        (string? appName, string? windowTitle) GetActiveWindow();

        /// <summary>Raised when focus moves to a different application.</summary>
        event EventHandler<AppWindowChangedEventArgs>? WindowChanged;

        void StartMonitoring();
        void StopMonitoring();
        bool IsMonitoring { get; }
    }
}
