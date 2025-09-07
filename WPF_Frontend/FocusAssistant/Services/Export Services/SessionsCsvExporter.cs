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
    public class SessionsCsvExporter : ISessionsExporter, ICsvExporter
    {
        private readonly ISessionRepository _sessionRepository;
        public SessionsCsvExporter(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task ExportAsync(string filePath)
        {
            await ExportSessionsAsync(filePath);
        }

        public async Task ExportSessionsAsync(string filePath, int days = 30)
        {
            var sessions = await _sessionRepository.GetSessionsAsync(days);
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            await writer.WriteLineAsync("SessionId,Date,StartTime,EndTime,Duration(min),ProductiveTime(min),DistractedTime(min),BreakTime(min),ProductivityScore,AppSwitches,TopApps");
            foreach (var session in sessions)
            {
                await writer.WriteLineAsync($"{session.SessionId}," +
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
            Console.WriteLine($"📄 Sessions CSV exported: {sessions.Count()} records to {filePath}");
        }
    }
}
