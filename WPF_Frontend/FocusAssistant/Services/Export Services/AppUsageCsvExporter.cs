using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Export_Services.Interfaces;
using FocusAssistant.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services
{
    public class AppUsageCsvExporter : IAppUsageExporter, ICsvExporter
    {
        private readonly ISessionRepository _sessionRepository;

        public AppUsageCsvExporter(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task ExportAsync(string filePath)
        {
            await ExportAppUsageAsync(filePath);
        }

        public async Task ExportAppUsageAsync(string filePath, int days = 30)
        {
            var sessions = await _sessionRepository.GetSessionsAsync(days);

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            // Header
            await writer.WriteLineAsync("SessionId,Timestamp,AppName,WindowTitle,Duration(min),IsProductive,DayOfWeek,HourOfDay");

            // Data rows
            foreach (var session in sessions)
            {
                foreach (var usage in session.AppUsages)
                {
                    await writer.WriteLineAsync($"{session.SessionId}," +
                                   $"{usage.StartTime:yyyy-MM-dd HH:mm:ss}," +
                                   $"\"{usage.AppName}\"," +
                                   $"\"{usage.WindowTitle?.Replace("\"", "''")}\"," +
                                   $"{usage.Duration.TotalMinutes:F1}," +
                                   $"{usage.IsProductive}," +
                                   $"{usage.StartTime.DayOfWeek}," +
                                   $"{usage.StartTime.Hour}");
                }
            }

            Console.WriteLine($"📱 App usage CSV exported to {filePath}");
        }
    }
}
