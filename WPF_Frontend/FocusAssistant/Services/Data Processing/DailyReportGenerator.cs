using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Datafetch.Interfaces;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Session
{
    /// <summary>
    /// Produces the daily summary shown on the Tracking view, preferring the
    /// backend's view of today and falling back to the local database.
    /// </summary>
    public class DailyReportGenerator : IReportGenerator
    {
        // The streak walk queries one day at a time, so it needs a hard stop.
        private const int MaxStreakDays = 365;

        private readonly IBaseService<UserSession> _userSessions;
        private readonly IAnalyticsService _analyticsService;

        public DailyReportGenerator(
            IBaseService<UserSession> userSessions,
            IAnalyticsService analyticsService)
        {
            _userSessions = userSessions ?? throw new ArgumentNullException(nameof(userSessions));
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        }

        /// <summary>
        /// Today's report from the backend, falling back to locally stored sessions
        /// when it is unreachable.
        /// </summary>
        public async Task<AnalyticsResponse> GetReportFlask()
        {
            try
            {
                var report = await _analyticsService.GetAnalyticsAsync();
                if (report is not null)
                    return report;

                Console.WriteLine("Backend analytics unavailable; using local session history.");
            }
            catch (Exception ex)
            {
                // A service must not raise UI. The previous version opened a
                // MessageBox from here, which also threw when called off the UI thread.
                Console.WriteLine($"Backend analytics failed: {ex.Message}. Using local session history.");
            }

            return await GenerateReportInternal(DateTime.Today);
        }

        /// <summary>Builds a report from sessions stored in the local database.</summary>
        public async Task<AnalyticsResponse> GenerateReportInternal(DateTime date)
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

                return new AnalyticsResponse
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    ProductivityRate = productivityRate,
                    RecentInterventions = sessions.Sum(s => s.DistractionEvents),
                    TopApps = appCounts,
                    TotalActivities = sessions.Sum(s => s.MostUsedApps.Count),
                    ProductivityStreaks = await CalculateStreakAsync(sessions, date),
                    Status = "success",
                    Timestamp = DateTime.Now.ToString("O"),
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating local report for {date:yyyy-MM-dd}: {ex.Message}");
                return new AnalyticsResponse
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    Status = "error",
                    Timestamp = DateTime.Now.ToString("O"),
                };
            }
        }

        private Task<List<UserSession>> GetSessionsForDateAsync(DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            return _userSessions.QueryAsync(q => q.Where(s => s.StartTime >= start && s.StartTime < end));
        }

        /// <summary>Consecutive days ending at <paramref name="date"/> with productive time.</summary>
        private async Task<int> CalculateStreakAsync(List<UserSession> todaysSessions, DateTime date)
        {
            if (!todaysSessions.Any(s => s.FocusTimeMinutes > 0))
                return 0;

            var streak = 1;
            var day = date.Date.AddDays(-1);

            // Bounded: the original loop was `while (true)` with a query per
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
