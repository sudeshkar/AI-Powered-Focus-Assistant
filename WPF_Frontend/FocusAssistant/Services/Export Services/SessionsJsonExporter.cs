using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Export_Services.Interfaces;
using FocusAssistant.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services
{
    public class SessionsJsonExporter : ISessionsExporter, IJsonExporter
    {
        private readonly ISessionRepository _sessionRepository;

        public SessionsJsonExporter(ISessionRepository sessionRepository)
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

            var exportData = new
            {
                export_date = DateTime.Now,
                total_sessions = sessions.Count(),
                date_range = new
                {
                    start = sessions.Any() ? sessions.Min(s => s.StartTime) : DateTime.Now,
                    end = sessions.Any() ? sessions.Max(s => s.EndTime) : DateTime.Now
                },
                sessions = sessions
            };

            string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

            Console.WriteLine($"📄 Sessions JSON exported: {sessions.Count()} records to {filePath}");
        }
    }
}
