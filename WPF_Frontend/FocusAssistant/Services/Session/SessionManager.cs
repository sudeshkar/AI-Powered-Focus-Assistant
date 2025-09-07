using FocusAssistant.Models;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Session
{
    public class SessionManager : ISessionManager
    {
        private readonly List<WorkSession> _allSessions;
        private WorkSession _currentSession;
        private readonly IActivityManagementService _activityManagementService;

        public event EventHandler<WorkSession> SessionStarted;
        public event EventHandler<WorkSession> SessionEnded;
        public event EventHandler<AppUsage> AppUsageAdded;
        public event EventHandler<WorkSession> SessionUpdated;

        public WorkSession CurrentSession => _currentSession;
        public bool IsSessionActive => _currentSession != null;
        public List<WorkSession> TodaySessions =>
            _allSessions.Where(s => s.StartTime.Date == DateTime.Today).ToList();

        public SessionManager(IActivityManagementService activityManagementService)
        {
            _allSessions = new List<WorkSession>();
            _activityManagementService = activityManagementService;
        }

        public void AddAppUsage(AppUsage appUsage)
        {
            if (appUsage == null || !IsSessionActive)
            {
                Console.WriteLine("⚠️ Attempted to add null AppUsage OR ⚠️ Cannot add app usage: No active session.");
                return;
            }

            _currentSession.AppUsages.Add(appUsage);
            AppUsageAdded?.Invoke(this, appUsage);
            Console.WriteLine($"📱 App tracked: {appUsage.AppName} ({appUsage.Duration.TotalSeconds:F1}s) - {(appUsage.IsProductive ? "✅ Productive" : "❌ Distracted")}");
        }

        public async Task EndSessionAsync()
        {
            if (!IsSessionActive)
            {
                Console.WriteLine("⚠️ No active session to end.");
                return;
            }

            _currentSession.EndTime = DateTime.Now;
            _currentSession.Duration = _currentSession.EndTime - _currentSession.StartTime;
            _currentSession.CalculateStatistics();

            SessionEnded?.Invoke(this, _currentSession);
            Console.WriteLine($"🏁 Session ended: {_currentSession.SessionId}");
            Console.WriteLine($"   Duration: {_currentSession.Duration:hh\\:mm\\:ss}");
            Console.WriteLine($"   Productivity: {_currentSession.ProductivityScore:F1}%");
            Console.WriteLine($"   App Switches: {_currentSession.AppSwitches}");

            try
            {
                Console.WriteLine($"Starting SaveSessionAsync at {DateTime.Now:HH:mm:ss.fff}");
                await _activityManagementService.SaveSessionAsync(_currentSession);
                Console.WriteLine($"Completed SaveSessionAsync at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveSessionAsync failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            _currentSession = null;
        }

        public void StartSession()
        {
            if (IsSessionActive)
            {
                Console.WriteLine("⚠️ Session already active. Ending current session first.");
                EndSessionAsync().GetAwaiter().GetResult(); // Synchronous for compatibility
            }

            _currentSession = new WorkSession
            {
                SessionId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now
            };

            _allSessions.Add(_currentSession);
            SessionStarted?.Invoke(this, _currentSession);
            Console.WriteLine($"✅ Session started: {_currentSession.SessionId}");
        }

        public SessionStatistics GetTodayStatistics()
        {
            var todaySessions = TodaySessions;
            if (!todaySessions.Any())
            {
                return new SessionStatistics();
            }

            var totalWorkTime = TimeSpan.FromTicks(todaySessions.Sum(s => s.Duration.Ticks));
            var totalProductiveTime = TimeSpan.FromTicks(todaySessions.Sum(s => s.ProductiveTime.Ticks));
            var totalBreakTime = TimeSpan.FromTicks(todaySessions.Sum(s => s.BreakTime.Ticks));
            var averageSessionLength = TimeSpan.FromTicks((long)todaySessions.Average(s => s.Duration.Ticks));
            var totalAppSwitches = todaySessions.Sum(s => s.AppSwitches);

            var productivityScore = totalWorkTime.TotalMinutes > 0
                ? (totalProductiveTime.TotalMinutes / totalWorkTime.TotalMinutes) * 100
                : 0;

            return new SessionStatistics
            {
                TotalSessions = todaySessions.Count,
                TotalWorkTime = totalWorkTime,
                TotalProductiveTime = totalProductiveTime,
                TotalBreakTime = totalBreakTime,
                AverageSessionLength = averageSessionLength,
                ProductivityScore = productivityScore,
                TotalAppSwitches = totalAppSwitches
            };
        }
    }
}