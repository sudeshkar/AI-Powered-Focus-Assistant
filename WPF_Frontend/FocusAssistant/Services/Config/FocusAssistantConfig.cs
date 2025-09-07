using FocusAssistant.Services.Config.interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Config
{
    public class FocusAssistantConfig : IFocusAssistantConfig
    {
        public string LogDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusAssistant", "Logs");
        public string FlaskApiUrl { get; set; } = "http://127.0.0.1:5000";
        public int IdleThresholdSeconds { get; set; } = 300;
        public TimeSpan TrackingInterval { get; set; } = TimeSpan.FromSeconds(2);
        public int? IdleTimeoutMinutes { get; set; } = 2;
    }
}
