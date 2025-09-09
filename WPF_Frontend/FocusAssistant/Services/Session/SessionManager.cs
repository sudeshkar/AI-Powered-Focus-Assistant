using FocusAssistant.Data;
using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Datafetch;
using FocusAssistant.Services.Datafetch.Interfaces;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Models.Events;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FocusAssistant.Services.Session
{
    public class SessionManager : ISessionManager
    {


        private readonly IBaseService<UserSession> _userSessionService;
        private UserSession _currentUserSession;
        private WorkSession _currentWorkSession;
        private List<WorkSession> _allWorkSessions;
        private AppUsage _currentAppUsage;

        public event EventHandler<UserSession> SessionStarted;
        public event EventHandler<UserSession> SessionEnded;
        
        public bool IsSessionActive => _currentUserSession != null;
        public List<WorkSession> TodaySessions =>
            _allWorkSessions.Where(s => s.StartTime.Date == DateTime.Today).ToList();

        private readonly IBaseService<WorkSession> _workSessionService;
        private readonly IBaseService<AppUsage> _appUsageService;
        private readonly IBaseService<RLInteraction> _rlInteractionService;
        private readonly IActivityService _rlService;
        private readonly IWindowMonitor _windowTracker;
        private readonly RuleBasedProductivityStrategy _ruleBasedProductivityStrategy;
        private readonly IIdleMonitor _idleMonitor;
        private DateTime? _breakStartTime;




        public SessionManager(
                    IBaseService<UserSession> userSessionService,
                    IBaseService<WorkSession> workSessionService,
                    IBaseService<AppUsage> appUsageService,
                    IBaseService<RLInteraction> rlInteractionService,
                    IActivityService rlService,
                    IWindowMonitor windowTracker,
                    RuleBasedProductivityStrategy ruleBasedProductivityStrategy,
                    IIdleMonitor idleMonitor)
                        {
                    _userSessionService = userSessionService;
                    _workSessionService = workSessionService;
                    _appUsageService = appUsageService;
                    _rlInteractionService = rlInteractionService;
                    _rlService = rlService;
                    _windowTracker = windowTracker;
                    _idleMonitor = idleMonitor;
                    _ruleBasedProductivityStrategy = ruleBasedProductivityStrategy;

                    _allWorkSessions = new List<WorkSession>();

                    // Subscribe to window change events
                    _windowTracker.WindowChanged += OnWindowChangedAsync;
            _idleMonitor.IdleStateChanged += _idleMonitor_IdleStateChanged;
                    
                    
        }

        private void _idleMonitor_IdleStateChanged(object? sender, IdleStateChangedEventArgs e)
        {
            if (_currentWorkSession == null) return;

            if (e.IsIdle)
            {
                // User went idle → start break
                _breakStartTime = e.ChangeTime;
                Console.WriteLine($"⏸ Break started at {_breakStartTime}");
            }
            else
            {
                // User became active → end break
                if (_breakStartTime.HasValue)
                {
                    var breakDuration = e.ChangeTime - _breakStartTime.Value;
                    _currentWorkSession.BreakTime += breakDuration; // accumulate break time
                    Console.WriteLine($"▶️ Break ended. Duration: {breakDuration.TotalMinutes:F1} min");

                    // Reset break start for next idle period
                    _breakStartTime = null;
                }
            }

        }

        private void StartAppUsage(string appName, string windowTitle, DateTime startTime)
        {
            _currentAppUsage = new AppUsage
            {
                AppName = appName,
                WindowTitle = windowTitle,
                StartTime = startTime,
                IsProductive = _ruleBasedProductivityStrategy.IsProductive(appName, windowTitle)
            };
        }
        public void AddAppUsage(string appname,AppUsage usage)
        {
            if (_currentWorkSession == null || usage == null) return;

            _currentWorkSession.AppUsages.Add(usage);
            _currentAppUsage = usage; // track the current app
            Console.WriteLine($"📱 Added App Usage: {usage.AppName} ({usage.Duration.TotalMinutes:F1} mins)");
        }


        private void FinalizeCurrentAppUsage()
        {
            if (_currentAppUsage == null) return;

            _currentAppUsage.EndTime = DateTime.Now;
            _currentAppUsage.Duration = _currentAppUsage.EndTime - _currentAppUsage.StartTime;
            _currentWorkSession.AppUsages.Add(_currentAppUsage);
            _currentAppUsage = null;
        }

        private async void OnWindowChangedAsync(object? sender, AppWindowChangedEventArgs e)
        {
            if (_currentWorkSession == null) return;

            // Close previous app usage
            if (_currentAppUsage != null)
            {
                _currentAppUsage.EndTime = e.ChangeTime;
                _currentAppUsage.Duration = _currentAppUsage.EndTime - _currentAppUsage.StartTime;
                _currentWorkSession.AppUsages.Add(_currentAppUsage);
            }

            // Create new app usage
            var newAppUsage = new AppUsage
            {
                wID = _currentWorkSession.wID,
                AppName = e.CurrentAppName,
                WindowTitle = e.CurrentWindowTitle,
                StartTime = e.ChangeTime,
                IsProductive = _ruleBasedProductivityStrategy.IsProductive(e.CurrentAppName, e.CurrentWindowTitle)
            };

            await _appUsageService.CreateAsync(newAppUsage);
            _currentAppUsage = newAppUsage;

            
            await _rlService.SendActivityAsync(_currentAppUsage);
        }

        public async Task EndSessionAsync()
        {
            if (_currentUserSession == null) return;

             
            // Close last app usage
            if (_currentAppUsage != null)
            {
                _currentAppUsage.EndTime = DateTime.Now;
                _currentAppUsage.Duration = _currentAppUsage.EndTime - _currentAppUsage.StartTime;
                _currentWorkSession.AppUsages.Add(_currentAppUsage);
                _currentAppUsage = null;
            }
            foreach (var usage in _currentWorkSession.AppUsages)
            {
                await _appUsageService.CreateAsync(usage);
                
            }
            // Finalize work session
            _currentWorkSession.EndTime = DateTime.Now;
            _currentWorkSession.CalculateStatistics();
            await _workSessionService.UpdateAsync(_currentWorkSession);

            // Finalize user session
            _currentUserSession.EndTime = DateTime.Now;
            await _userSessionService.UpdateAsync(_currentUserSession);

            SessionEnded?.Invoke(this, _currentUserSession);

            _currentUserSession = null;
            _currentWorkSession = null;
        }

        public async Task StartSessionAsync()
        {
            if (IsSessionActive)
            {
                Console.WriteLine("⚠️ Session already active. Ending current session first.");
                await EndSessionAsync();
            }


            _currentUserSession = new UserSession();
            _currentWorkSession = new WorkSession
            {
                
                StartTime = DateTime.Now
            };

            Console.WriteLine($"✅ Session started: {_currentUserSession.SessionId}");
            await _userSessionService.CreateAsync(_currentUserSession);
            await _workSessionService.CreateAsync(_currentWorkSession);

            SessionStarted?.Invoke(this, _currentUserSession);
        }
        public void AddAppUsage(string appName, TimeSpan durationMinutes)
        {
            if (_currentWorkSession == null) return;

            var appUsage = new AppUsage
            {
                AppName = appName,
                Duration = durationMinutes
            };

            _currentWorkSession.AppUsages.Add(appUsage);
            Console.WriteLine($"📱 Added App Usage: {appName} ({durationMinutes} mins)");
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