using FocusAssistant.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public class LoggingService
    {
        private readonly string _logDirectory;
        private readonly string _currentSessionFile;

        public LoggingService()
        {
            _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       "FocusAssistant", "Logs");

            Directory.CreateDirectory(_logDirectory);

            string dateString = DateTime.Now.ToString("yyyy-MM-dd");
            _currentSessionFile = Path.Combine(_logDirectory, $"session_{dateString}.json");
        }

        public void SaveSession(List<AppUsage> appUsages)
        {
            try
            {
                var sessionData = new
                {
                    SessionId = Guid.NewGuid().ToString(),
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    StartTime = appUsages.FirstOrDefault()?.StartTime ?? DateTime.Now,
                    EndTime = appUsages.LastOrDefault()?.EndTime ?? DateTime.Now,
                    TotalApps = appUsages.Count,
                    TotalDuration = appUsages.Sum(a => a.Duration.TotalMinutes),
                    ProductiveTime = appUsages.Where(a => a.IsProductive).Sum(a => a.Duration.TotalMinutes),
                    DistractedTime = appUsages.Where(a => !a.IsProductive).Sum(a => a.Duration.TotalMinutes),
                    AppUsages = appUsages
                };

                string json = JsonConvert.SerializeObject(sessionData, Formatting.Indented);

                // Save to daily file
                File.WriteAllText(_currentSessionFile, json);

                // Also append to master log
                SaveToMasterLog(sessionData);

                Console.WriteLine($"💾 Session saved: {appUsages.Count} app switches logged");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving session: {ex.Message}");
            }
        }

        private void SaveToMasterLog(object sessionData)
        {
            try
            {
                string masterLogFile = Path.Combine(_logDirectory, "master_log.json");
                List<object> allSessions = new List<object>();

                // Load existing sessions
                if (File.Exists(masterLogFile))
                {
                    string existingJson = File.ReadAllText(masterLogFile);
                    allSessions = JsonConvert.DeserializeObject<List<object>>(existingJson) ?? new List<object>();
                }

                // Add new session
                allSessions.Add(sessionData);

                // Keep only last 30 days
                // You can implement date filtering here if needed

                string json = JsonConvert.SerializeObject(allSessions, Formatting.Indented);
                File.WriteAllText(masterLogFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving to master log: {ex.Message}");
            }
        }

        public List<object> LoadRecentSessions(int days = 7)
        {
            try
            {
                string masterLogFile = Path.Combine(_logDirectory, "master_log.json");

                if (!File.Exists(masterLogFile))
                    return new List<object>();

                string json = File.ReadAllText(masterLogFile);
                var allSessions = JsonConvert.DeserializeObject<List<object>>(json) ?? new List<object>();

                // Filter by date if needed
                return allSessions.TakeLast(days * 3).ToList(); // Assume ~3 sessions per day
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading sessions: {ex.Message}");
                return new List<object>();
            }
        }

        public void SaveRealTimeLog(AppUsage appUsage)
        {
            try
            {
                string realTimeFile = Path.Combine(_logDirectory, "realtime.json");
                string logEntry = JsonConvert.SerializeObject(new
                {
                    Timestamp = DateTime.Now,
                    AppName = appUsage.AppName,
                    WindowTitle = appUsage.WindowTitle,
                    Duration = appUsage.Duration.TotalSeconds,
                    IsProductive = appUsage.IsProductive
                });

                File.AppendAllText(realTimeFile, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving real-time log: {ex.Message}");
            }
        }
        public void SaveWorkSession(WorkSession session)
        {
            try
            {
                string sessionsFile = Path.Combine(_logDirectory, "work_sessions.json");
                List<WorkSession> allSessions = new List<WorkSession>();

                // Load existing sessions
                if (File.Exists(sessionsFile))
                {
                    string existingJson = File.ReadAllText(sessionsFile);
                    allSessions = JsonConvert.DeserializeObject<List<WorkSession>>(existingJson) ?? new List<WorkSession>();
                }

                // Add new session
                allSessions.Add(session);

                // Keep only last 30 days
                var cutoffDate = DateTime.Now.AddDays(-30);
                allSessions = allSessions.Where(s => s.StartTime >= cutoffDate).ToList();

                string json = JsonConvert.SerializeObject(allSessions, Formatting.Indented);
                File.WriteAllText(sessionsFile, json);

                Console.WriteLine($"💾 Work session saved: {session.SessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving work session: {ex.Message}");
            }
        }

        public List<WorkSession> LoadWorkSessions(int days = 7)
        {
            try
            {
                string sessionsFile = Path.Combine(_logDirectory, "work_sessions.json");

                if (!File.Exists(sessionsFile))
                    return new List<WorkSession>();

                string json = File.ReadAllText(sessionsFile);
                var allSessions = JsonConvert.DeserializeObject<List<WorkSession>>(json) ?? new List<WorkSession>();

                // Filter by date
                var cutoffDate = DateTime.Now.AddDays(-days);
                return allSessions.Where(s => s.StartTime >= cutoffDate).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading work sessions: {ex.Message}");
                return new List<WorkSession>();
            }
        }

        public void ExportToCsv(List<WorkSession> sessions, string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("SessionId,Date,StartTime,EndTime,Duration(min),ProductiveTime(min),DistractedTime(min),BreakTime(min),ProductivityScore,AppSwitches,TopApps");

                    // Write session data
                    foreach (var session in sessions)
                    {
                        writer.WriteLine($"{session.SessionId}," +
                                       $"{session.StartTime:yyyy-MM-dd}," +
                                       $"{session.StartTime:HH:mm:ss}," +
                                       $"{session.EndTime:HH:mm:ss}," +
                                       $"{session.Duration.TotalMinutes:F1}," +
                                       $"{session.ProductiveTime.TotalMinutes:F1}," +
                                       $"{session.DistractedTime.TotalMinutes:F1}," +
                                       $"{session.BreakTime.TotalMinutes:F1}," +
                                       $"{session.ProductivityScore:F1}," +
                                       $"{session.AppSwitches}," +
                                       $"\"{string.Join("; ", session.TopApps)}\"");
                    }
                }

                Console.WriteLine($"📊 Data exported to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error exporting to CSV: {ex.Message}");
            }
        }

        public void SaveIdleLog(IdleStateChangedEventArgs idleEvent)
        {
            try
            {
                string idleFile = Path.Combine(_logDirectory, "idle_log.json");
                var idleEntry = new
                {
                    Timestamp = idleEvent.StateChangeTime,
                    IsIdle = idleEvent.IsIdle,
                    Duration = idleEvent.IdleDuration.TotalSeconds,
                    State = idleEvent.IsIdle ? "IDLE_START" : "ACTIVE_RESUME"
                };

                string logEntry = JsonConvert.SerializeObject(idleEntry);
                File.AppendAllText(idleFile, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving idle log: {ex.Message}");
            }
        }
    }
}
