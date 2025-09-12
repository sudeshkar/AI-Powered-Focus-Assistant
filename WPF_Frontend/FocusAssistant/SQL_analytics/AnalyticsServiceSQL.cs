using FocusAssistant.Data;
using FocusAssistant.Models;
using FocusAssistant.Services.Datafetch;
using FocusAssistant.Services.Datafetch.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.SQL_analytics
{
    public class AnalyticsServiceSQL
    {
        private readonly IBaseService<WorkSession> _workSessionService; 
        private readonly IBaseService<AppUsage> _appUsageService;


        public AnalyticsServiceSQL(IBaseService<WorkSession> workSessionService, IBaseService<AppUsage> appUsageService)
        {
            _workSessionService = workSessionService ?? throw new ArgumentNullException(nameof(workSessionService));
            _appUsageService = appUsageService ?? throw new ArgumentNullException(nameof(appUsageService));
        }

        public async Task<SessionStatistics> GetDailyStatisticsAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Get all WorkSessions for the date
            var workSessionIds = await _workSessionService.GetAllAsync();
            var filteredSessions = workSessionIds
                .Where(ws => ws.StartTime >= startOfDay && ws.StartTime <= endOfDay)
                .ToList();

            // For each filtered session, load AppUsages using the service
            var statistics = new SessionStatistics
            {
                TotalSessions = filteredSessions.Count,
                TotalWorkTime = TimeSpan.FromTicks(filteredSessions.Sum(ws => ws.Duration.Ticks)),
                TotalProductiveTime = TimeSpan.FromTicks(filteredSessions.Sum(ws => ws.ProductiveTime.Ticks)),
                TotalDistractedTime = TimeSpan.FromTicks(filteredSessions.Sum(ws => ws.DistractedTime.Ticks)),
                TotalBreakTime = TimeSpan.FromTicks(filteredSessions.Sum(ws => ws.BreakTime.Ticks)),
                TotalAppSwitches = filteredSessions.Sum(ws => ws.AppSwitches),
                ProductivityScore = filteredSessions.Any() ? filteredSessions.Average(ws => ws.ProductivityScore) : 0
            };

            if (statistics.TotalWorkTime.TotalMinutes > 0)
            {
                statistics.AverageSessionLength = TimeSpan.FromTicks(statistics.TotalWorkTime.Ticks / Math.Max(1, statistics.TotalSessions));
            }

            return statistics;
        }

        public async Task<List<(string AppName, TimeSpan Duration)>> GetTopAppsAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Get all AppUsages
            var allAppUsages = await _appUsageService.GetAllAsync();
            var filteredAppUsages = allAppUsages
                .Where(au => au.StartTime >= startOfDay && au.StartTime <= endOfDay)
                .ToList();

            var appUsages = filteredAppUsages
                .GroupBy(au => au.AppName)
                .Select(g => new
                {
                    AppName = g.Key,
                    Duration = TimeSpan.FromTicks(g.Sum(au => au.Duration.Ticks))
                })
                .OrderByDescending(g => g.Duration)
                .Take(5)
                .ToList();

            return appUsages.Select(au => (au.AppName, au.Duration)).ToList();
        }

        public async Task<string> GenerateCsvReportAsync(DateTime date)
        {
            var stats = await GetDailyStatisticsAsync(date);
            var topApps = await GetTopAppsAsync(date);

            var csv = new StringBuilder();
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Date,{date:yyyy-MM-dd}");
            csv.AppendLine($"Total Sessions,{stats.TotalSessions}");
            csv.AppendLine($"Total Work Time,{stats.TotalWorkTime.TotalHours:F2} hours");
            csv.AppendLine($"Productive Time,{stats.TotalProductiveTime.TotalHours:F2} hours");
            csv.AppendLine($"Distracted Time,{stats.TotalDistractedTime.TotalHours:F2} hours");
            csv.AppendLine($"Break Time,{stats.TotalBreakTime.TotalHours:F2} hours");
            csv.AppendLine($"Average Session Length,{stats.AverageSessionLength.TotalMinutes:F2} minutes");
            csv.AppendLine($"Productivity Score,{stats.ProductivityScore:F2}%");
            csv.AppendLine($"Total App Switches,{stats.TotalAppSwitches}");
            csv.AppendLine();
            csv.AppendLine("Top Apps,Duration (hours)");
            foreach (var app in topApps)
            {
                csv.AppendLine($"{app.AppName},{app.Duration.TotalHours:F2}");
            }

            return csv.ToString();
        }
    }
}



