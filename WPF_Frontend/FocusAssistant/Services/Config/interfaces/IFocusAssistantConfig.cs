using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Config.interfaces
{
    public interface IFocusAssistantConfig
    {
        string LogDirectory { get; }
        string FlaskApiUrl { get; }
        int IdleThresholdSeconds { get; }
        TimeSpan TrackingInterval { get; }
        int? IdleTimeoutMinutes { get; set; }
    }
}
