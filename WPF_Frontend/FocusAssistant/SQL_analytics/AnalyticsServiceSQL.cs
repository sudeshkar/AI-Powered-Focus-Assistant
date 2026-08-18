using FocusAssistant.Models;
using FocusAssistant.Services.Datafetch.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.SQL_analytics
{
    /// <summary>
    /// Daily aggregates read straight from the local database, independent of the
    /// Python backend.
    /// </summary>
    public class AnalyticsServiceSQL
    {
        private readonly IBaseService<WorkSession> _workSessions;
        private readonly IBaseService<AppUsage> _appUsages;

        public AnalyticsServiceSQL(
            IBaseService<WorkSession> workSessions,
            IBaseService<AppUsage> appUsages)
        {
            _workSessions = workSessions ?? throw new ArgumentNullException(nameof(workSessions));
            _appUsages = appUsages ?? throw new ArgumentNullException(nameof(appUsages));
        }

        public async Task<SessionStatistics> GetDailyStatisticsAsync(DateTime date)
        {
            var (start, end) = DayBounds(date);

            // Filtered in SQL. This used to call GetAllAsync() and filter in memory,
            // loading every row ever recorded on each view refresh.
            var sessions = await _workSessions.QueryAsync(q => q
                .Where(s => s.StartTime >= start && s.StartTime < end));

            if (sessions.Count == 0)
                return new SessionStatistics();

            var totalWork = TimeSpan.FromTicks(sessions.Sum(s => s.Duration.Ticks));
            var totalProductive = TimeSpan.FromTicks(sessions.Sum(s => s.ProductiveTime.Ticks));

            return new SessionStatistics
            {
                TotalSessions = sessions.Count,
                TotalWorkTime = totalWork,
                TotalProductiveTime = totalProductive,
                TotalDistractedTime = TimeSpan.FromTicks(sessions.Sum(s => s.DistractedTime.Ticks)),
                TotalBreakTime = TimeSpan.FromTicks(sessions.Sum(s => s.BreakTime.Ticks)),
                TotalAppSwitches = sessions.Sum(s => s.AppSwitches),
                AverageSessionLength = TimeSpan.FromTicks(totalWork.Ticks / sessions.Count),
                // Weighted by time rather than averaging per-session percentages,
                // so a one-minute session no longer counts as much as an eight-hour one.
                ProductivityScore = totalWork.TotalMinutes > 0
                    ? totalProductive.TotalMinutes / totalWork.TotalMinutes * 100
                    : 0,
            };
        }

        public async Task<List<AppUsageSummary>> GetTopAppsAsync(DateTime date, int take = 5)
        {
            var (start, end) = DayBounds(date);

            var usages = await _appUsages.QueryAsync(q => q
                .Where(u => u.StartTime >= start && u.StartTime < end)
                .Select(u => new { u.AppName, u.Duration }));

            return usages
                .GroupBy(u => u.AppName)
                .Select(g => new AppUsageSummary(
                    g.Key,
                    TimeSpan.FromTicks(g.Sum(u => u.Duration.Ticks))))
                .OrderByDescending(a => a.Duration)
                .Take(take)
                .ToList();
        }

        public async Task<string> GenerateCsvReportAsync(DateTime date)
        {
            var stats = await GetDailyStatisticsAsync(date);
            var topApps = await GetTopAppsAsync(date);

            var csv = new StringBuilder();
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Date,{date:yyyy-MM-dd}");
            csv.AppendLine($"Total Sessions,{stats.TotalSessions}");
            csv.AppendLine($"Total Work Time (hours),{stats.TotalWorkTime.TotalHours:F2}");
            csv.AppendLine($"Productive Time (hours),{stats.TotalProductiveTime.TotalHours:F2}");
            csv.AppendLine($"Distracted Time (hours),{stats.TotalDistractedTime.TotalHours:F2}");
            csv.AppendLine($"Break Time (hours),{stats.TotalBreakTime.TotalHours:F2}");
            csv.AppendLine($"Average Session Length (minutes),{stats.AverageSessionLength.TotalMinutes:F2}");
            csv.AppendLine($"Productivity Score (%),{stats.ProductivityScore:F2}");
            csv.AppendLine($"Total App Switches,{stats.TotalAppSwitches}");
            csv.AppendLine();
            csv.AppendLine("Application,Duration (hours)");
            foreach (var app in topApps)
                csv.AppendLine($"{EscapeCsv(app.AppName)},{app.Duration.TotalHours:F2}");

            return csv.ToString();
        }

        private static (DateTime start, DateTime end) DayBounds(DateTime date) =>
            (date.Date, date.Date.AddDays(1));

        /// <summary>Quotes fields containing separators so window titles cannot break the file.</summary>
        private static string EscapeCsv(string? value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }

    /// <summary>Total time spent in one application.</summary>
    public record AppUsageSummary(string AppName, TimeSpan Duration);
}
