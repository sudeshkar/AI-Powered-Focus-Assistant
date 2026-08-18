using FocusAssistant.Services.Models.Events;
using System;

namespace FocusAssistant.Services.Application_Monitoring.Interfaces
{
    /// <summary>Watches whether the user has stopped interacting with the machine.</summary>
    public interface IIdleMonitor
    {
        bool IsIdle { get; }
        TimeSpan CurrentIdleTime { get; }

        /// <summary>Raised when the user goes idle or becomes active again.</summary>
        event EventHandler<IdleStateChangedEventArgs>? IdleStateChanged;

        void StartMonitoring();
        void StopMonitoring();
        bool IsMonitoring { get; }
    }
}
