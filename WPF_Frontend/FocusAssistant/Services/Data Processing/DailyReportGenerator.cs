using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Datafetch.Interfaces;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FocusAssistant.Services.Session
{
    public class DailyReportGenerator : IReportGenerator
    {
        private readonly IBaseService<UserSession> _userSession;
        private readonly IFlaskServerManager _flaskServerManager;
        private readonly IAnalyticsService _analyticsService;

        public DailyReportGenerator(
            IBaseService<UserSession> userSession,
            IFlaskServerManager flaskServerManager,
            IAnalyticsService analyticsService)
        {
            _userSession = userSession;
            _flaskServerManager = flaskServerManager;
            _analyticsService = analyticsService;
        }

        // Fetch report from Flask API
        public async Task<AnalyticsResponse> GetReportFlask(AnalyticsResponse analyticsResponse)
        {
            try
            {
                return await _analyticsService.GetAnalyticsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // If Flask fails, fallback to local report generation
                return await GenerateReportInternal(DateTime.Now);
            }
        }

        // Generate a local report if Flask is not available
        public async Task<AnalyticsResponse> GenerateReportInternal(DateTime date)
        {
            try
            {
                if (_userSession is not IUserSessionService userSessionService)
                {
                    throw new InvalidOperationException("UserSession service does not support GetByDateAsync");
                }

                // Ensure sessions are generic IEnumerable<UserSession>
                var sessionsRaw = await userSessionService.GetByDateAsync(date);
                var sessions = sessionsRaw.Cast<UserSession>().ToList();

                if (!sessions.Any())
                {
                    return new AnalyticsResponse
                    {
                        Date = date.ToString("yyyy-MM-dd"),
                        ProductivityRate = 0,
                        RecentInterventions = 0,
                        TopApps = new List<string>(),
                        TotalActivities = 0,
                        Status = "no_data",
                        Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.ffffff")
                    };
                }

                // Calculate productive and break time
                var totalProductiveTicks = sessions.Sum(s => s.FocusTimeMinutes * TimeSpan.TicksPerMinute);
                var totalBreakTicks = sessions.Sum(s =>
                    (s.EndTime - s.StartTime).Ticks - (s.FocusTimeMinutes * TimeSpan.TicksPerMinute));
                var totalTimeTicks = totalProductiveTicks + totalBreakTicks;

                var productivityRate = totalTimeTicks > 0
                    ? Math.Round((double)totalProductiveTicks / totalTimeTicks * 100, 2)
                    : 0.0;

                // Get top 5 most used apps
                var topApps = sessions
                    .SelectMany(s => s.MostUsedApps ?? new List<string>())
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList();

                // Build final analytics report
                var report = new AnalyticsResponse
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    ProductivityRate = productivityRate,
                    RecentInterventions = sessions.Sum(s => s.DistractionEvents),
                    TopApps = topApps,
                    TotalActivities = sessions.SelectMany(s => s.MostUsedApps ?? new List<string>()).Count(),
                    Status = "success",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.ffffff")
                };

                // Calculate productivity streaks
                report.productivityStreaks = await CalculateStreak(sessions);

                return report;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating report for {date:yyyy-MM-dd}: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                return new AnalyticsResponse
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    ProductivityRate = 0,
                    RecentInterventions = 0,
                    TopApps = new List<string>(),
                    TotalActivities = 0,
                    Status = "error",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.ffffff")
                };
            }
        }

        // Calculate productivity streak
        private async Task<int> CalculateStreak(IList<UserSession> sessions)
        {
            if (sessions == null || !sessions.Any())
                return 0;

            // Check if today has productive sessions
            bool isTodayProductive = sessions.Any(s => s.FocusTimeMinutes > 0);
            if (!isTodayProductive)
                return 0;

            int streak = 1;
            var previousDay = sessions.First().StartTime.Date.AddDays(-1);

            if (_userSession is not IUserSessionService userSessionService)
            {
                throw new InvalidOperationException("UserSession service does not support GetByDateAsync");
            }

            while (true)
            {
                var prevSessionsRaw = await userSessionService.GetByDateAsync(previousDay);
                var prevSessions = prevSessionsRaw.Cast<UserSession>().ToList();

                if (!prevSessions.Any(s => s.FocusTimeMinutes > 0))
                    break;

                streak++;
                previousDay = previousDay.AddDays(-1);
            }

            return streak;
        }
    }
}
