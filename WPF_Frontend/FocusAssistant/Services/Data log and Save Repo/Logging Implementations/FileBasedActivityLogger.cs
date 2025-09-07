using FocusAssistant.Models;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using FocusAssistant.Services.Models.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Logging_Implementations
{
    public class FileBasedActivityLogger: IActivityLogger
    {
        private readonly IFileSystemWrapper _fileSystem;
        private readonly ILoggingService _loggingService;
        private readonly string _logDirectory;
        private readonly string _realTimeFile;
        private readonly string _idleFile;

        public FileBasedActivityLogger(
            IFileSystemWrapper fileSystem,
            ILoggingService loggingService)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));

            _logDirectory = _fileSystem.CombinePath(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusAssistant", "Logs");
            _fileSystem.CreateDirectory(_logDirectory);

            _realTimeFile = _fileSystem.CombinePath(_logDirectory, "realtime.json");
            _idleFile = _fileSystem.CombinePath(_logDirectory, "idle_log.json");
        }

        public async Task LogActivitiesAsync(IEnumerable<AppUsage> activities)
        {
            if (activities == null || !activities.Any())
            {
                _loggingService.LogWarning("Attempted to log null or empty activities collection");
                return;
            }

            try
            {
                var tasks = activities.Select(LogActivityAsync);
                await Task.WhenAll(tasks);

                _loggingService.LogInformation($"Successfully logged {activities.Count()} activities");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error logging multiple activities", ex);
                throw;
            }
        }

        public async Task LogActivityAsync(AppUsage activity)
        {
            if (activity == null)
            {
                _loggingService.LogWarning("Attempted to log null activity");
                return;
            }

            try
            {
                var logEntry = new
                {
                    Timestamp = DateTime.Now,
                    activity.AppName,
                    activity.WindowTitle,
                    Duration = activity.Duration.TotalSeconds,
                    activity.IsProductive
                };

                string json = JsonConvert.SerializeObject(logEntry);
                await _fileSystem.AppendAllTextAsync(_realTimeFile, json + Environment.NewLine);

                _loggingService.LogDebug($"Activity logged: {activity.AppName} for {activity.Duration.TotalSeconds}s");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error logging activity for {activity?.AppName}", ex);
                throw;
            }
        }

        public async Task LogIdleStateAsync(IdleStateChangedEventArgs idleEvent)
        {
            if (idleEvent == null)
            {
                _loggingService.LogWarning("Attempted to log null idle event");
                return;
            }

            try
            {
                var idleEntry = new
                {
                    Timestamp = idleEvent.ChangeTime,
                    idleEvent.IsIdle,
                    Duration = idleEvent.IdleTime.TotalSeconds,
                    State = idleEvent.IsIdle ? "IDLE_START" : "ACTIVE_RESUME"
                };

                string json = JsonConvert.SerializeObject(idleEntry);
                await _fileSystem.AppendAllTextAsync(_idleFile, json + Environment.NewLine);

                _loggingService.LogDebug($"Idle state logged: {idleEntry.State}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error logging idle state", ex);
                throw;
            }
        }
    }
}
