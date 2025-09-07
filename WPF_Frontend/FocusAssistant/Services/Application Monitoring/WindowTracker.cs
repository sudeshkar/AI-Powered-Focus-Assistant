using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Models.Events;
using FocusAssistant.Services.Session.Interfaces;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Application_Monitoring
{
    public class WindowTracker : IDisposable
    {
        private readonly IWindowMonitor _windowMonitor;
        private readonly IIdleMonitor _idleMonitor;
        private readonly ISessionManager _sessionManager;
        private readonly IProductivityClassifier _productivityClassifier;
        private readonly IActivityService _activityService;
        private readonly IFeedbackService _feedbackService;
        private readonly IActivityManagementService _activityManager;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private DateTime _lastSwitchTime;
        public AppUsage _currentAppUsage;
        private bool _isTracking = false;

        public event EventHandler<AppUsage> AppSwitched;
        public event EventHandler<List<AppUsage>> SessionCompleted;
        public event EventHandler<ActivityResponse> AiInterventionReceived;

        public bool IsTracking => _isTracking;
        public WorkSession CurrentSession => _sessionManager.CurrentSession;

        public WindowTracker(
            IWindowMonitor windowMonitor,
            IIdleMonitor idleMonitor,
            ISessionManager sessionManager,
            IProductivityClassifier productivityClassifier,
            IActivityService activityService,
            IFeedbackService feedbackService,
            IActivityManagementService activityManagementService)
        {
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _idleMonitor = idleMonitor ?? throw new ArgumentNullException(nameof(idleMonitor));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _productivityClassifier = productivityClassifier ?? throw new ArgumentNullException(nameof(productivityClassifier));
            _activityService = activityService ?? throw new ArgumentNullException(nameof(activityService));
            _feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));
            _activityManager = activityManagementService ?? throw new ArgumentNullException(nameof(activityManagementService));

            _windowMonitor.WindowChanged += OnWindowChanged;
            _idleMonitor.IdleStateChanged += OnIdleStateChanged;
        }

        public async Task StartTrackingAsync()
        {
            if (_isTracking)
            {
                Console.WriteLine("WindowTracker already tracking, skipping start.");
                return;
            }

            _isTracking = true;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                _windowMonitor.StartMonitoring();
                _idleMonitor.StartMonitoring();
                _sessionManager.StartSession();
                var (appName, windowTitle) = _windowMonitor.GetActiveWindow();
                if (!string.IsNullOrEmpty(appName))
                {
                    _currentAppUsage = CreateAppUsage(appName, windowTitle);
                    _lastSwitchTime = DateTime.Now;
                    _sessionManager.AddAppUsage(_currentAppUsage);
                    Console.WriteLine($"Initial app usage set: {appName}, {windowTitle}");
                }
                else
                {
                    Console.WriteLine("Warning: No initial app detected by WindowMonitor");
                }
                Console.WriteLine("Window tracking started...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartTracking error: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                _isTracking = false;
                throw;
            }
        }

        public async Task StopTrackingAsync()
        {
            if (!_isTracking)
            {
                Console.WriteLine("WindowTracker not tracking, skipping stop.");
                return;
            }

            _isTracking = false;

            try
            {
                _cts?.Cancel();
                FinalizeCurrentAppUsage();

                // Stop monitors asynchronously
                var stopWindowTask = Task.Run(() => _windowMonitor.StopMonitoring());
                var stopIdleTask = Task.Run(() => _idleMonitor.StopMonitoring());
                await Task.WhenAll(stopWindowTask, stopIdleTask);

                // Save session in the background
                var sessionAppUsages = _sessionManager.CurrentSession?.AppUsages ?? new List<AppUsage>();
                if (sessionAppUsages.Any())
                {
                    await Task.Run(() => _activityManager.SaveSessionFromActivitiesAsync(sessionAppUsages));
                }

                _sessionManager.EndSessionAsync();
                SessionCompleted?.Invoke(this, sessionAppUsages);

                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _currentAppUsage = null;
                Console.WriteLine("Window tracking stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StopTracking error: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                throw;
            }
        }

        private void OnWindowChanged(object sender, AppWindowChangedEventArgs e)
        {
            Console.WriteLine($"WindowChanged detected at {DateTime.Now:HH:mm:ss.fff}: App={e.CurrentAppName}, Title={e.CurrentWindowTitle}");
            if (!_isTracking || _idleMonitor.IsIdle) return;

            FinalizeCurrentAppUsage();

            if (!string.IsNullOrEmpty(e.CurrentAppName))
            {
                _currentAppUsage = CreateAppUsage(e.CurrentAppName, e.CurrentWindowTitle);
                _lastSwitchTime = DateTime.Now;
                _sessionManager.AddAppUsage(_currentAppUsage);
                Console.WriteLine($"New app usage started: {e.CurrentAppName}, {e.CurrentWindowTitle}");
            }
        }

        private void OnIdleStateChanged(object sender, IdleStateChangedEventArgs e)
        {
            if (!_isTracking) return;

            if (e.IsIdle)
            {
                FinalizeCurrentAppUsage();
                Console.WriteLine("User is idle, pausing tracking...");
            }
            else
            {
                var (appName, windowTitle) = _windowMonitor.GetActiveWindow();
                if (!string.IsNullOrEmpty(appName))
                {
                    _currentAppUsage = CreateAppUsage(appName, windowTitle);
                    _sessionManager.AddAppUsage(_currentAppUsage);
                }
                Console.WriteLine("User is active, resuming tracking...");
            }
        }

        private AppUsage CreateAppUsage(string appName, string windowTitle)
        {
            return new AppUsage
            {
                AppName = appName ?? "Unknown",
                WindowTitle = windowTitle ?? "Unknown",
                StartTime = DateTime.Now,
                IsProductive = _productivityClassifier.IsProductiveActivity(appName ?? "", windowTitle ?? "")
            };
        }

        private void FinalizeCurrentAppUsage()
        {
            if (_currentAppUsage == null)
            {
                Console.WriteLine($"Current App Usage Null at {DateTime.Now:HH:mm:ss.fff}");
                return;
            }

            _currentAppUsage.EndTime = DateTime.Now;
            _currentAppUsage.Duration = _currentAppUsage.EndTime - _lastSwitchTime;
            _currentAppUsage.IsProductive = _productivityClassifier.IsProductiveActivity(_currentAppUsage.AppName, _currentAppUsage.WindowTitle);

            if (_currentAppUsage.Duration.TotalSeconds >= 1)
            {
                AppSwitched?.Invoke(this, _currentAppUsage);
                Console.WriteLine($"AppSwitched event raised for: {_currentAppUsage.AppName}, duration: {_currentAppUsage.Duration.TotalSeconds:F3}s");
                Task.Run(() => NotifyAiAsync(_currentAppUsage, _cts.Token));
            }
            _currentAppUsage = null;
        }

        private async Task NotifyAiAsync(AppUsage usage, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (usage == null)
                {
                    Console.WriteLine("NotifyAiAsync called with null usage");
                    return;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

                var response = await _activityService.SendActivityAsync(usage).WithCancellation(linkedCts.Token);

                if (response.Status == "error" || string.IsNullOrEmpty(response.InterventionMessage))
                {
                    Console.WriteLine("AI response was null or collecting data");
                    if (new Random().NextDouble() < 0.1)
                    {
                        new ToastContentBuilder()
                            .AddText("Focus Assistant")
                            .AddText("AI is learning from your activities. Keep going!")
                            .Show();
                    }
                    return;
                }

                await _activityManager.LogActivityAsync(usage);
                AiInterventionReceived?.Invoke(this, response);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("NotifyAiAsync canceled or timed out");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI prediction failed: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_isTracking)
                {
                    await StopTrackingAsync();
                    Console.WriteLine($"StopTracking called during DisposeAsync at {DateTime.Now:HH:mm:ss.fff}");
                }
                _windowMonitor.WindowChanged -= OnWindowChanged;
                _idleMonitor.IdleStateChanged -= OnIdleStateChanged;
                _cts?.Dispose();
                Console.WriteLine($"WindowTracker disposed at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DisposeAsync error: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
            }
        }

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }

    public static class TaskExtensions
    {
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

            var completedTask = await Task.WhenAny(task, tcs.Task);
            if (completedTask == tcs.Task)
                throw new OperationCanceledException(cancellationToken);

            return await task;
        }
    }
}