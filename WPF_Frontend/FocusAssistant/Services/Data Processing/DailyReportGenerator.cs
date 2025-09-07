using FocusAssistant.Models;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Session
{
    public class DailyReportGenerator : IReportGenerator
    {
        private readonly ISessionRepository _sessionRepository;

        public DailyReportGenerator(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task<DailyReport> GenerateReportAsync(DateTime date)
        {
            try
            {
                var sessions = await _sessionRepository.GetSessionsByDateAsync(date);
                var report = new DailyReport
                {
                    Date = date,
                    ProductiveTime = TimeSpan.FromTicks(sessions.Sum(s => s.ProductiveTime.Ticks)),
                    DistractedTime = TimeSpan.FromTicks(sessions.Sum(s => s.BreakTime.Ticks)),
                    ProductivityStreak = 0,
                    TopApps = sessions.SelectMany(s => s.AppUsages)
                                     .GroupBy(a => a.AppName)
                                     .Select(g => new { AppName = g.Key, Duration = g.Sum(a => a.Duration.TotalMinutes) })
                                     .OrderByDescending(a => a.Duration)
                                     .Take(5)
                                     .ToDictionary(a => a.AppName, a => a.Duration)
                };
                return report;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating report: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                return new DailyReport { Date = date };
            }
        }

        private int CalculateStreak(IList<WorkSession> sessions)
        {
            // Placeholder: Implement streak logic
            return sessions.Any() ? 1 : 0;
        }
    }
}