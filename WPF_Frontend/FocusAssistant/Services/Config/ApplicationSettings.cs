using FocusAssistant.Services.Config.interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Config
{
    public class ApplicationSettings : IApplicationSettings
    {
        public int DataRetentionDays { get; init; } = 90;
        public bool EnableFileLogging { get; init; } = true;
        public bool EnableDatabaseLogging { get; init; } = true;
        public string LogLevel { get; init; } = "Information";
        public int MaxCacheSize { get; init; } = 1000;
        public TimeSpan CacheExpiration { get; init; } = TimeSpan.FromMinutes(15);
    }
}
