using FocusAssistant.Models;
using FocusAssistant.Services.Export_Services.Interfaces;
using FocusAssistant.Services.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services
{
    public class DailyReportsCsvExporter : IDailyReportsExporter, ICsvExporter
    {
        private readonly IReportGenerator _reportGenerator;
        public DailyReportsCsvExporter(IReportGenerator reportGenerator)
        {
            _reportGenerator = reportGenerator;
        }
        public async Task ExportAsync(string filePath)
        {
            await ExportDailyReportsAsync(filePath);
        }

        public async Task ExportDailyReportsAsync(string filePath, int days = 30)
        {
            var reports = new List<DailyProductivityReport>();

            for (int i = 0; i < days; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                var report = _reportGenerator.GenerateReportAsync(date);
                if (report.NumberOfSessions > 0)
                    reports.Add(report);
            }

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            // Header
            await writer.WriteLineAsync("Date,TotalWorkTime(h),ProductiveTime(h),DistractedTime(h),BreakTime(h),ProductivityScore,TotalAppSwitches,AvgSessionLength(min),NumberOfSessions,MostProductiveHour,LeastProductiveHour");

            // Data rows
            foreach (var report in reports.OrderBy(r => r.Date))
            {
                await writer.WriteLineAsync($"{report.Date:yyyy-MM-dd}," +
                               $"{report.TotalWorkTimeHours:F2}," +
                               $"{report.ProductiveTimeHours:F2}," +
                               $"{report.DistractedTimeHours:F2}," +
                               $"{report.BreakTimeHours:F2}," +
                               $"{report.ProductivityScore:F1}," +
                               $"{report.TotalAppSwitches}," +
                               $"{report.AverageSessionLength:F1}," +
                               $"{report.NumberOfSessions}," +
                               $"\"{report.MostProductiveHour}\"," +
                               $"\"{report.LeastProductiveHour}\"");
            }

            Console.WriteLine($"📈 Daily reports CSV exported: {reports.Count} days to {filePath}");
        }
    }
}
