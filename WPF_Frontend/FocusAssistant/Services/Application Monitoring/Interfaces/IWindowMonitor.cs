using FocusAssistant.Services.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Application_Monitoring.Interfaces
{
    public interface IWindowMonitor
    {
        (string appName, string windowTitle) GetActiveWindow();
        event EventHandler<AppWindowChangedEventArgs> WindowChanged;
        void StartMonitoring();
        void StopMonitoring();
        bool IsMonitoring { get; }
    }
}
