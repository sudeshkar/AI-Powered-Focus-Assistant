using FocusAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public class SessionManager
    {
        private List<WorkSession> _sessions;
        private WorkSession _currentSession;
        private readonly LoggingService _loggingService;
        private readonly IdleTimeDetector _idleDetector;
        private bool _isSessionActive = false;

        public event EventHandler<WorkSession> SessionStarted;
        public event EventHandler<WorkSession> SessionEnded;
        public event EventHandler<WorkSession> SessionUpdated;

        public WorkSession CurrentSession => _currentSession;
        public List<WorkSession> TodaySessions => _sessions?.Where(s => s.StartTime.Date == DateTime.Today).ToList();
        public bool IsSessionActive => _isSessionActive;

        public SessionManager(LoggingService loggingService, IdleTimeDetector idleDetector)
        {
            _loggingService = loggingService;
            _idleDetector = idleDetector;
            _sessions = new List<WorkSession>();

            // Subscribe to idle state changes
            _idleDetector.IdleStateChanged += OnIdleStateChanged;
        }

        public void StartSession()
        {
            if (_isSessionActive) return;

            _currentSession = new WorkSession
            {
                SessionId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now,
                AppUsages = new List<AppUsage>()
            };

            _isSessionActive = true;
            _sessions.Add(_currentSession);

            SessionStarted?.Invoke(this, _currentSession);
            Console.WriteLine($"🚀 Work session started: {_currentSession.SessionId}");
        }

        public void EndSession()
        {
            if (!_isSessionActive || _currentSession == null) return;

            _currentSession.EndTime = DateTime.Now;
            _currentSession.Duration = _currentSession.EndTime - _currentSession.StartTime;
            _currentSession.CalculateStatistics();

            _isSessionActive = false;

            // Save session data
            _loggingService.SaveSession(_currentSession.AppUsages);
            _loggingService.SaveWorkSession(_currentSession);

            SessionEnded?.Invoke(this, _currentSession);
            Console.WriteLine($"⏹️ Work session ended: {FormatDuration(_currentSession.Duration)}");
        }

        public void AddAppUsage(AppUsage appUsage)
        {
            if (!_isSessionActive || _currentSession == null) return;

            _currentSession.AppUsages.Add(appUsage);
            _currentSession.CalculateStatistics();

            SessionUpdated?.Invoke(this, _currentSession);
        }

        private void OnIdleStateChanged(object sender, IdleStateChangedEventArgs e)
        {
            if (_currentSession == null) return;

            if (e.IsIdle)
            {
                // User went idle - record break start
                _currentSession.BreakTime += e.IdleDuration;
                Console.WriteLine($"💤 Break detected: {FormatDuration(e.IdleDuration)}");
            }
            else
            {
                // User became active again
                Console.WriteLine($"⚡ User active again after {FormatDuration(e.IdleDuration)}");
            }

            _currentSession.CalculateStatistics();
            SessionUpdated?.Invoke(this, _currentSession);
        }

        public SessionStatistics GetTodayStatistics()
        {
            var todaySessions = TodaySessions;
            if (!todaySessions.Any()) return new SessionStatistics();

            return new SessionStatistics
            {
                TotalSessions = todaySessions.Count,
                TotalWorkTime = TimeSpan.FromTicks(todaySessions.Sum(s => s.Duration.Ticks)),
                TotalProductiveTime = TimeSpan.FromTicks(todaySessions.Sum(s => s.ProductiveTime.Ticks)),
                TotalBreakTime = TimeSpan.FromTicks(todaySessions.Sum(s => s.BreakTime.Ticks)),
                AverageSessionLength = TimeSpan.FromTicks((long)todaySessions.Average(s => s.Duration.Ticks)),
                ProductivityScore = todaySessions.Average(s => s.ProductivityScore),
                TotalAppSwitches = todaySessions.Sum(s => s.AppSwitches)
            };
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            else
                return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
        }
    }
}
