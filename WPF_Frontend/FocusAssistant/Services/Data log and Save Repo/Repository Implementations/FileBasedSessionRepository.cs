using FocusAssistant.Models;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Repository_Implementations
{
    public class FileBasedSessionRepository : ISessionRepository
    {
        private readonly IFileSystemWrapper _fileSystem;
        private readonly ILoggingService _loggingService;
        private readonly string _dataDirectory;
        private readonly string _sessionsFile;

        public FileBasedSessionRepository(
            IFileSystemWrapper fileSystem,
            ILoggingService loggingService)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));

            _dataDirectory = _fileSystem.CombinePath(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusAssistant", "Data");
            _fileSystem.CreateDirectory(_dataDirectory);

            _sessionsFile = _fileSystem.CombinePath(_dataDirectory, "sessions.json");
        }

        public async Task DeleteSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            try
            {
                if (!_fileSystem.FileExists(_sessionsFile))
                {
                    _loggingService.LogWarning("Cannot delete session: sessions file does not exist");
                    return;
                }

                string json = await _fileSystem.ReadAllTextAsync(_sessionsFile);
                var allSessions = JsonConvert.DeserializeObject<List<WorkSession>>(json) ?? new List<WorkSession>();

                int removedCount = allSessions.RemoveAll(s => s.SessionId == sessionId);

                if (removedCount > 0)
                {
                    string updatedJson = JsonConvert.SerializeObject(allSessions, Formatting.Indented);
                    await _fileSystem.WriteAllTextAsync(_sessionsFile, updatedJson);
                    _loggingService.LogInformation($"Session deleted: {sessionId}");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error deleting session: {sessionId}", ex);
                throw;
            }
        }

        public async Task<WorkSession?> GetSessionByIdAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;

            try
            {
                var sessions = await GetSessionsAsync(30);
                return sessions.FirstOrDefault(s => s.SessionId == sessionId);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error getting session by ID: {sessionId}", ex);
                return null;
            }
        }

        public async Task<IEnumerable<WorkSession>> GetSessionsAsync(int days = 7)
        {
            try
            {
                if (!_fileSystem.FileExists(_sessionsFile))
                {
                    _loggingService.LogInformation("Sessions file does not exist, returning empty collection");
                    return Enumerable.Empty<WorkSession>();
                }

                string json = await _fileSystem.ReadAllTextAsync(_sessionsFile);
                var allSessions = JsonConvert.DeserializeObject<List<WorkSession>>(json) ?? new List<WorkSession>();

                var cutoffDate = DateTime.Now.AddDays(-days);
                return allSessions.Where(s => s.StartTime >= cutoffDate);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error loading work sessions for last {days} days", ex);
                return Enumerable.Empty<WorkSession>();
            }
        }

        public async Task<IEnumerable<WorkSession>> GetSessionsByDateAsync(DateTime date)
        {
            try
            {
                if (!_fileSystem.FileExists(_sessionsFile))
                    return Enumerable.Empty<WorkSession>();

                string json = await _fileSystem.ReadAllTextAsync(_sessionsFile);
                var allSessions = JsonConvert.DeserializeObject<List<WorkSession>>(json) ?? new List<WorkSession>();

                return allSessions.Where(s => s.StartTime.Date == date.Date);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error loading work sessions for {date:yyyy-MM-dd}", ex);
                return Enumerable.Empty<WorkSession>();
            }
        }

        public async Task SaveSessionAsync(WorkSession session)
        {
            if (session == null)
            {
                _loggingService.LogWarning("Attempted to save null session");
                return;
            }

            try
            {
                var allSessions = new List<WorkSession>();

                if (_fileSystem.FileExists(_sessionsFile))
                {
                    string existingJson = await _fileSystem.ReadAllTextAsync(_sessionsFile);
                    allSessions = JsonConvert.DeserializeObject<List<WorkSession>>(existingJson) ?? new List<WorkSession>();
                }

                allSessions.RemoveAll(s => s.SessionId == session.SessionId);
                allSessions.Add(session);

                var cutoffDate = DateTime.Now.AddDays(-30);
                allSessions = allSessions.Where(s => s.StartTime >= cutoffDate).ToList();

                string json = JsonConvert.SerializeObject(allSessions, Formatting.Indented);
                await _fileSystem.WriteAllTextAsync(_sessionsFile, json);

                _loggingService.LogInformation($"Work session saved: {session.SessionId}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error saving work session: {session?.SessionId}", ex);
                throw;
            }
        }
    }
}
