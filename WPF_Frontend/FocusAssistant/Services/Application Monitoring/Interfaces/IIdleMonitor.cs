using FocusAssistant.Services.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Application_Monitoring.Interfaces
{
    public interface IIdleMonitor
    {
        bool IsIdle { get; }
        TimeSpan CurrentIdleTime { get; }
        event EventHandler<IdleStateChangedEventArgs> IdleStateChanged;
        void StartMonitoring();
        void StopMonitoring();
        bool IsMonitoring { get; }
    }
}
