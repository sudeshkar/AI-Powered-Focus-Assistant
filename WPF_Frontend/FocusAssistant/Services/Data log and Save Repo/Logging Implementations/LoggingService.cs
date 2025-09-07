using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Logging_Implementations
{
    public class LoggingService : ILoggingService
    {
        private readonly IFileSystemWrapper _fileSystem;
        private readonly string _logDirectory;

        public LoggingService(IFileSystemWrapper fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

            _logDirectory = _fileSystem.CombinePath(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusAssistant", "Logs");
            _fileSystem.CreateDirectory(_logDirectory);
        }

        public void LogDebug(string message) => _ = WriteToFileAsync("DEBUG", message);
        public void LogInformation(string message) => _ = WriteToFileAsync("INFO", message);
        public void LogWarning(string message) => _ = WriteToFileAsync("WARN", message);
        public void LogError(string message, Exception? exception = null) =>
            _ = WriteToFileAsync("ERROR", message, exception);

        private async Task WriteToFileAsync(string level, string message, Exception? exception = null)
        {
            try
            {
                var logFile = _fileSystem.CombinePath(_logDirectory, $"app_log_{DateTime.Now:yyyy-MM-dd}.txt");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

                if (exception != null)
                    logEntry += $"\nException: {exception}";

                await _fileSystem.AppendAllTextAsync(logFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // Fail silently to avoid infinite logging loops
            }
        }
    }
}
