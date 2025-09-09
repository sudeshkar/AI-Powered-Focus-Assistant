using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FocusAssistant.Services.Session
{
    public class DailyReportGenerator : IReportGenerator
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IFlaskServerManager _flaskServerManager;
        private readonly IAnalyticsService _analyticsService;

        public DailyReportGenerator(ISessionRepository sessionRepository, IFlaskServerManager flaskServerManager, IAnalyticsService analyticsService)
        {
            _sessionRepository = sessionRepository;
            _flaskServerManager = flaskServerManager;
            _analyticsService = analyticsService;
        }


        public async Task<AnalyticsResponse> GetReportFlask(AnalyticsResponse analyticsResponse)
        {
            try
            {
                return await _analyticsService.GetAnalyticsAsync();
                
               

            }
            catch (Exception ex) {

                MessageBox.Show(
                $"An error occurred: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

                return await GenerateReportInternal(DateTime.Now);
            }

        }

        public async Task<AnalyticsResponse> GenerateReportInternal(DateTime date)
        {
            try
            {
                var sessions = await _sessionRepository.GetSessionsByDateAsync(date);
                var totalProductiveTicks = sessions.Sum(s => s.ProductiveTime.Ticks);
                var totalBreakTicks = sessions.Sum(s => s.BreakTime.Ticks);
                var totalTimeTicks = totalProductiveTicks + totalBreakTicks;

                var productivityRate = totalTimeTicks > 0
                    ? Math.Round((double)totalProductiveTicks / totalTimeTicks * 100, 2)
                    : 0.0;

                var topApps = sessions.SelectMany(s => s.AppUsages)
                    .GroupBy(a => a.AppName)
                    .Select(g => new { AppName = g.Key, Duration = (int)g.Sum(a => a.Duration.TotalSeconds) })
                    .OrderByDescending(a => a.Duration)
                    .Take(5)
                    .ToDictionary(a => a.AppName, a => a.Duration);

                var report = new AnalyticsResponse
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    ProductivityRate = productivityRate,
                    RecentInterventions = sessions.Sum(s => s.Interventions?.Count ?? 0), 
                    TopApps = topApps,
                    TotalActivities = sessions.SelectMany(s => s.AppUsages).Count(),
                    Status = "success",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.ffffff")
                };

                report.productivityStreaks = CalculateStreak(sessions.ToList());
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
                    TopApps = new Dictionary<string, int>(),
                    TotalActivities = 0,
                    Status = "error",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.ffffff")
                };
            }
        }

        private int CalculateStreak(IList<WorkSession> sessions)
        {
            if (!sessions.Any())
                return 0;

            // Check if today has productive sessions
            bool isTodayProductive = sessions.Any(s => s.ProductiveTime > TimeSpan.Zero);
            if (!isTodayProductive)
                return 0;

            // Check previous days for streak (simplified logic)
            int streak = 1;
            var previousDay = sessions.First().StartTime.Date.AddDays(-1);

            // Assuming repository can fetch sessions for previous days
            while (true)
            {
                var prevSessions = _sessionRepository.GetSessionsByDateAsync(previousDay).Result;
                if (!prevSessions.Any(s => s.ProductiveTime > TimeSpan.Zero))
                    break;
                streak++;
                previousDay = previousDay.AddDays(-1);
            }

            return streak;
        }
    }
}