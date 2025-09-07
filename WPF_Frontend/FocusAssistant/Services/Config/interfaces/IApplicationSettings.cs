using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Config.interfaces
{
    public interface IApplicationSettings
    {
        int DataRetentionDays { get; }
        bool EnableFileLogging { get; }
        bool EnableDatabaseLogging { get; }
        string LogLevel { get; }
        int MaxCacheSize { get; }
        TimeSpan CacheExpiration { get; }
    }
}
