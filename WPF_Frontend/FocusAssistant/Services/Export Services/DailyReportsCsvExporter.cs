using FocusAssistant.Models;
using FocusAssistant.Services.Datafetch.Interfaces;
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
        private readonly IWorkSessionService _worksession;
        public DailyReportsCsvExporter(IReportGenerator reportGenerator, IWorkSessionService workSession)
        {
            _reportGenerator = reportGenerator;
            _worksession = workSession;
        }
        public async Task ExportAsync(string filePath)
        {
            await ExportDailyReportsAsync(filePath);
        }

        public async Task ExportDailyReportsAsync(string filepath,int days = 30)
        {
            var reports = new List<DailyProductivityReport>();

            for (int i = 0; i < days; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                var workSessions = await _worksession.GetByDateAsync(date);
                if (!workSessions.Any())
                    continue;
                var dailyReport = GenerateDailyReportFromSessions(date, workSessions);
                reports.Add(dailyReport);
                 
            }

            using var writer = new StreamWriter(filepath, false, Encoding.UTF8);

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

            Console.WriteLine($"📈 Daily reports CSV exported: {reports.Count} days to {filepath}");
        }

        private DailyProductivityReport GenerateDailyReportFromSessions(DateTime date, IEnumerable<WorkSession> sessions)
        {
            // Flatten all AppUsage entries from all sessions
            var allAppUsages = sessions.SelectMany(s => s.AppUsages).ToList();

            var totalWorkTime = sessions.Sum(s => (s.EndTime - s.StartTime).TotalHours);
            var productiveTime = allAppUsages.Where(a => a.IsProductive).Sum(a => a.Duration.TotalHours);
            var distractedTime = totalWorkTime - productiveTime;

            var breakTime = sessions.Sum(s => s.BreakTime.TotalHours); // Assuming BreakDuration exists

            var totalAppSwitches = allAppUsages.Count;
            var averageSessionLength = sessions.Any()
                ? sessions.Average(s => (s.EndTime - s.StartTime).TotalMinutes)
                : 0;

            // Top productive apps
            var topProductiveApps = allAppUsages
                .Where(a => a.IsProductive)
                .GroupBy(a => a.AppName)
                .OrderByDescending(g => g.Sum(a => a.Duration.TotalMinutes))
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            // Top distracting apps
            var topDistractingApps = allAppUsages
                .Where(a => !a.IsProductive)
                .GroupBy(a => a.AppName)
                .OrderByDescending(g => g.Sum(a => a.Duration.TotalMinutes))
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            // Productivity by hour
            var productivityByHour = allAppUsages
                .GroupBy(a => a.StartTime.Hour)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(a => a.IsProductive ? a.Duration.TotalMinutes : 0) /
                         Math.Max(g.Sum(a => a.Duration.TotalMinutes), 1) * 100
                );

            var mostProductiveHour = productivityByHour.OrderByDescending(x => x.Value).FirstOrDefault().Key;
            var leastProductiveHour = productivityByHour.OrderBy(x => x.Value).FirstOrDefault().Key;

            return new DailyProductivityReport
            {
                Date = date,
                TotalWorkTimeHours = totalWorkTime,
                ProductiveTimeHours = productiveTime,
                DistractedTimeHours = distractedTime,
                BreakTimeHours = breakTime,
                ProductivityScore = totalWorkTime > 0 ? (productiveTime / totalWorkTime) * 100 : 0,
                TotalAppSwitches = totalAppSwitches,
                AverageSessionLength = averageSessionLength,
                NumberOfSessions = sessions.Count(),
                TopProductiveApps = topProductiveApps,
                TopDistractingApps = topDistractingApps,
                ProductivityByHour = productivityByHour,
                MostProductiveHour = $"{mostProductiveHour}:00",
                LeastProductiveHour = $"{leastProductiveHour}:00",
                AppUsageMinutes = allAppUsages.Sum(a => a.Duration.TotalMinutes).ToString()+"M",
            };
        
        }
    }
}
