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
    }
}
