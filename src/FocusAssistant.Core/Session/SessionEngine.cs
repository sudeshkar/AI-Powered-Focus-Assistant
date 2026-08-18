using FocusAssistant.Core.Data.Abstractions;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Models;
using FocusAssistant.Core.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Session
{
    /// <summary>
    /// Owns the lifecycle of a tracking session: accumulates per-application usage
    /// as the user switches windows and persists everything when the session ends.
    /// </summary>
    /// <remarks>
    /// This used to also call out to a Python RL backend on every window switch -
    /// a network round trip per app switch, several times a minute. That
    /// dependency is gone: SessionEngine only tracks usage and raises
    /// <see cref="ActivityRecorded"/> for each completed stretch. Deciding whether
    /// and how to intervene belongs to the intervention pipeline (Phase 4), on
    /// device, with no network call anywhere in the loop.
    /// </remarks>
    public class SessionEngine : ISessionEngine, IDisposable
    {
        // Ignore blips: alt-tabbing through windows should not litter the database
        // or spam ActivityRecorded once per keystroke-triggered title change.
        private static readonly TimeSpan MinimumUsageDuration = TimeSpan.FromSeconds(2);

        private readonly IBaseService<UserSession> _userSessions;
        private readonly IBaseService<WorkSession> _workSessions;
        private readonly IBaseService<AppUsage> _appUsages;
        private readonly IWindowMonitor _windowMonitor;
        private readonly IIdleMonitor _idleMonitor;
        private readonly IProductivityStrategy _productivityStrategy;

        private readonly object _sessionLock = new();

        private UserSession? _currentUserSession;
        private WorkSession? _currentWorkSession;
        private AppUsage? _currentAppUsage;
        private DateTime? _breakStartTime;
        private bool _disposed;

        // Today's completed sessions, seeded from the database on start. The
        // original version never populated this, so GetTodayStatistics always
        // reported zeros.
        private List<WorkSession> _todayWorkSessions = new();

        public event EventHandler<UserSession>? SessionStarted;
        public event EventHandler<UserSession>? SessionEnded;
        public event EventHandler<AppUsage>? ActivityRecorded;

        public bool IsSessionActive
        {
            get { lock (_sessionLock) return _currentUserSession is not null; }
        }

        public string? CurrentGoal { get; private set; }

        public SessionEngine(
            IBaseService<UserSession> userSessions,
            IBaseService<WorkSession> workSessions,
            IBaseService<AppUsage> appUsages,
            IWindowMonitor windowMonitor,
            IIdleMonitor idleMonitor,
            IProductivityStrategy productivityStrategy)
        {
            _userSessions = userSessions ?? throw new ArgumentNullException(nameof(userSessions));
            _workSessions = workSessions ?? throw new ArgumentNullException(nameof(workSessions));
            _appUsages = appUsages ?? throw new ArgumentNullException(nameof(appUsages));
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _idleMonitor = idleMonitor ?? throw new ArgumentNullException(nameof(idleMonitor));
            _productivityStrategy = productivityStrategy ?? throw new ArgumentNullException(nameof(productivityStrategy));

            _windowMonitor.WindowChanged += OnWindowChanged;
            _idleMonitor.IdleStateChanged += OnIdleStateChanged;
        }

        public async Task StartSessionAsync(string? goal = null)
        {
            if (IsSessionActive)
            {
                Console.WriteLine("Session already active; ending it before starting a new one.");
                await EndSessionAsync();
            }

            var userSession = new UserSession { StartTime = DateTime.Now };
            var workSession = new WorkSession
            {
                StartTime = DateTime.Now,
                // Set the foreign key rather than the navigation property: assigning
                // the navigation made the first insert cascade the work session in,
                // and the follow-up insert then tried to add it a second time.
                SessionId = userSession.SessionId,
                TopAppsJson = "[]",
            };

            await _userSessions.CreateAsync(userSession);
            await _workSessions.CreateAsync(workSession);

            var todaysSessions = await LoadTodaysSessionsAsync();

            lock (_sessionLock)
            {
                _currentUserSession = userSession;
                _currentWorkSession = workSession;
                _currentAppUsage = null;
                _breakStartTime = null;
                _todayWorkSessions = todaysSessions;
                CurrentGoal = string.IsNullOrWhiteSpace(goal) ? null : goal.Trim();
            }

            // Start counting the app already in the foreground rather than waiting
            // for the next switch.
            var (appName, windowTitle) = _windowMonitor.GetActiveWindow();
            if (!string.IsNullOrEmpty(appName))
                BeginAppUsage(appName, windowTitle, DateTime.Now);

            Console.WriteLine($"Session started: {userSession.SessionId}" +
                (CurrentGoal is null ? "" : $" (goal: {CurrentGoal})"));
            SessionStarted?.Invoke(this, userSession);
        }

        public async Task EndSessionAsync()
        {
            UserSession? userSession;
            WorkSession? workSession;
            List<AppUsage> pendingUsages;

            lock (_sessionLock)
            {
                if (_currentUserSession is null || _currentWorkSession is null)
                    return;

                userSession = _currentUserSession;
                workSession = _currentWorkSession;

                CloseCurrentAppUsage(DateTime.Now);

                pendingUsages = workSession.AppUsages.Where(u => u.aID == 0).ToList();

                workSession.EndTime = DateTime.Now;
                workSession.Duration = workSession.EndTime - workSession.StartTime;
                workSession.CalculateStatistics();

                userSession.EndTime = DateTime.Now;
                userSession.FocusTimeMinutes = (int)workSession.ProductiveTime.TotalMinutes;
                userSession.ProductivityScore = workSession.ProductivityScore;
                userSession.MostUsedApps = workSession.TopApps;

                _todayWorkSessions.Add(workSession);
                _currentUserSession = null;
                _currentWorkSession = null;
                CurrentGoal = null;
            }

            try
            {
                // One round trip rather than a SaveChanges per usage row.
                await _appUsages.CreateRangeAsync(pendingUsages);
                await _workSessions.UpdateAsync(workSession);
                await _userSessions.UpdateAsync(userSession);
                Console.WriteLine($"Session saved: {pendingUsages.Count} app usages.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save session: {ex.Message} ({ex.InnerException?.Message})");
            }

            SessionEnded?.Invoke(this, userSession);
        }

        private async Task<List<WorkSession>> LoadTodaysSessionsAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            try
            {
                // Filtered in SQL rather than loading the whole table.
                return await _workSessions.QueryAsync(q => q
                    .Where(s => s.StartTime >= today && s.StartTime < tomorrow));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not load today's sessions: {ex.Message}");
                return new List<WorkSession>();
            }
        }

        private void OnIdleStateChanged(object? sender, IdleStateChangedEventArgs e)
        {
            lock (_sessionLock)
            {
                if (_currentWorkSession is null)
                    return;

                if (e.IsIdle)
                {
                    // Idle time is not work time: close the current usage so the
                    // duration does not absorb the whole break.
                    CloseCurrentAppUsage(e.ChangeTime);
                    _breakStartTime = e.ChangeTime;
                }
                else if (_breakStartTime.HasValue)
                {
                    _currentWorkSession.BreakTime += e.ChangeTime - _breakStartTime.Value;
                    _breakStartTime = null;

                    var (appName, windowTitle) = _windowMonitor.GetActiveWindow();
                    if (!string.IsNullOrEmpty(appName))
                        BeginAppUsageCore(appName, windowTitle, e.ChangeTime);
                }
            }
        }

        private void OnWindowChanged(object? sender, AppWindowChangedEventArgs e)
        {
            // Invoked from a timer thread. Nothing may escape, or an unhandled
            // exception on a threadpool thread takes the process down.
            try
            {
                lock (_sessionLock)
                {
                    if (_currentWorkSession is null)
                        return;

                    CloseCurrentAppUsage(e.ChangeTime);
                    BeginAppUsageCore(e.CurrentAppName, e.CurrentWindowTitle, e.ChangeTime);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling window change: {ex.Message}");
            }
        }

        /// <summary>Starts tracking a new foreground application. Takes the lock.</summary>
        private void BeginAppUsage(string appName, string? windowTitle, DateTime startTime)
        {
            lock (_sessionLock)
            {
                BeginAppUsageCore(appName, windowTitle, startTime);
            }
        }

        /// <summary>Caller must hold the session lock.</summary>
        private void BeginAppUsageCore(string appName, string? windowTitle, DateTime startTime)
        {
            if (_currentWorkSession is null)
                return;

            _currentAppUsage = new AppUsage
            {
                wID = _currentWorkSession.wID,
                AppName = appName,
                WindowTitle = windowTitle ?? string.Empty,
                StartTime = startTime,
                IsProductive = _productivityStrategy.IsProductive(appName, windowTitle),
            };
        }

        /// <summary>Caller must hold the session lock.</summary>
        private void CloseCurrentAppUsage(DateTime endTime)
        {
            if (_currentAppUsage is null || _currentWorkSession is null)
                return;

            var usage = _currentAppUsage;
            _currentAppUsage = null;

            usage.EndTime = endTime;
            usage.Duration = endTime - usage.StartTime;

            if (usage.Duration < MinimumUsageDuration)
                return;

            _currentWorkSession.AppUsages.Add(usage);

            // Raised from inside the lock, which is fine as long as subscribers
            // stay fast (queue work, do not block). The intervention pipeline
            // (Phase 4) reacts to this instead of SessionEngine calling out itself.
            ActivityRecorded?.Invoke(this, usage);
        }

        public SessionStatistics GetTodayStatistics()
        {
            List<WorkSession> sessions;
            lock (_sessionLock)
            {
                sessions = _todayWorkSessions
                    .Where(s => s.StartTime.Date == DateTime.Today)
                    .ToList();

                // Include the session in progress so the UI updates live.
                if (_currentWorkSession is not null)
                    sessions.Add(_currentWorkSession);
            }

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
                AverageSessionLength = TimeSpan.FromTicks(totalWork.Ticks / sessions.Count),
                TotalAppSwitches = sessions.Sum(s => s.AppSwitches),
                ProductivityScore = totalWork.TotalMinutes > 0
                    ? totalProductive.TotalMinutes / totalWork.TotalMinutes * 100
                    : 0,
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _windowMonitor.WindowChanged -= OnWindowChanged;
            _idleMonitor.IdleStateChanged -= OnIdleStateChanged;
        }
    }
}
