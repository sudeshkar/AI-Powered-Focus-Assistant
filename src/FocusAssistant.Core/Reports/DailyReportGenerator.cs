using FocusAssistant.Core.Data.Abstractions;
using FocusAssistant.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Reports
{
    /// <summary>
    /// Builds the daily focus report entirely from the local database. This used
    /// to prefer a report fetched from the Python backend and fall back to local
    /// data only when that call failed; there is no backend anymore, so this is
    /// simply the report.
    /// </summary>
    public class DailyReportGenerator : IReportGenerator
    {
        // The streak walk queries one day at a time, so it needs a hard stop.
        private const int MaxStreakDays = 365;

        private readonly IBaseService<UserSession> _userSessions;

        public DailyReportGenerator(IBaseService<UserSession> userSessions)
        {
            _userSessions = userSessions ?? throw new ArgumentNullException(nameof(userSessions));
        }

        public Task<DailyFocusReport> GetTodayReportAsync() => GenerateReportAsync(DateTime.Today);

        public async Task<DailyFocusReport> GenerateReportAsync(DateTime date)
        {
            try
            {
                var sessions = await GetSessionsForDateAsync(date);

                var productiveTicks = sessions.Sum(s => s.FocusTimeMinutes * TimeSpan.TicksPerMinute);
                var totalTicks = sessions.Sum(s => Math.Max(0, (s.EndTime - s.StartTime).Ticks));

                var productivityRate = totalTicks > 0
                    ? Math.Round((double)productiveTicks / totalTicks * 100, 2)
                    : 0.0;

                var appCounts = sessions
                    .SelectMany(s => s.MostUsedApps)
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToDictionary(g => g.Key, g => g.Count());

                return new DailyFocusReport
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    ProductivityRate = productivityRate,
                    RecentInterventions = sessions.Sum(s => s.DistractionEvents),
                    TopApps = appCounts,
                    TotalActivities = sessions.Sum(s => s.MostUsedApps.Count),
                    ProductivityStreakDays = await CalculateStreakAsync(sessions, date),
                    Status = "success",
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating report for {date:yyyy-MM-dd}: {ex.Message}");
                return new DailyFocusReport
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    Status = "error",
                };
            }
        }

        private Task<List<UserSession>> GetSessionsForDateAsync(DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            return _userSessions.QueryAsync(q => q.Where(s => s.StartTime >= start && s.StartTime < end));
        }

        /// <summary>Consecutive days ending at the given date with productive time.</summary>
        private async Task<int> CalculateStreakAsync(List<UserSession> todaysSessions, DateTime date)
        {
            if (!todaysSessions.Any(s => s.FocusTimeMinutes > 0))
                return 0;

            var streak = 1;
            var day = date.Date.AddDays(-1);

            // Bounded: a previous version used an unbounded loop with a query per
            // iteration and no exit other than a gap in the data.
            for (var i = 0; i < MaxStreakDays; i++, day = day.AddDays(-1))
            {
                var sessions = await GetSessionsForDateAsync(day);
                if (!sessions.Any(s => s.FocusTimeMinutes > 0))
                    break;

                streak++;
            }

            return streak;
        }
    }
}
