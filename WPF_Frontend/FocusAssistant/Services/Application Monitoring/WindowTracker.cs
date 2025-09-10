using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Datafetch.Interfaces;
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
    public class WindowTracker  
    {
        // Dependencies
        private readonly IWindowMonitor _windowMonitor;
        private readonly IIdleMonitor _idleMonitor;
        private readonly ISessionManager _sessionManager;

        // Internal state
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isTracking = false;


        // Public properties
        public bool IsTracking => _isTracking;
        

        public WindowTracker(
            IWindowMonitor windowMonitor,
            IIdleMonitor idleMonitor,
            ISessionManager sessionManager
        )
        {
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _idleMonitor = idleMonitor ?? throw new ArgumentNullException(nameof(idleMonitor));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));


          
        }

        #region Start / Stop Tracking
        public async Task StartTrackingAsync()
        {
            if (_isTracking)
            {
                Console.WriteLine("⚠️ WindowTracker already tracking, skipping start.");
                return;
            }

            _isTracking = true;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                await _sessionManager.StartSessionAsync();

                _windowMonitor.StartMonitoring();
                _idleMonitor.StartMonitoring();
                

                // Get initial active window
                //var (appName, windowTitle) = _windowMonitor.GetActiveWindow();
                //if (!string.IsNullOrEmpty(appName))
                //{
                //    _currentAppUsage = CreateAppUsage(appName, windowTitle);
                //    _lastSwitchTime = DateTime.Now;
                //    _sessionManager.AddAppUsage(appName, _currentAppUsage);

                //    Console.WriteLine($"✅ Initial app usage detected: {appName}, {windowTitle}");
                //}
                //else
                //{
                //    Console.WriteLine("⚠️ No initial app detected by WindowMonitor");
                //}

                Console.WriteLine("✅ Window tracking started successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ StartTrackingAsync error: {ex.Message}");
                _isTracking = false;
                throw;
            }
        }

        public async Task StopTrackingAsync()
        {
            if (!_isTracking)
            {
                Console.WriteLine("⚠️ WindowTracker is not tracking, skipping stop.");
                return;
            }

            _isTracking = false;

            try
            {
                _cts?.Cancel();
                 

                // Stop monitors asynchronously
                await Task.WhenAll(
                    Task.Run(() => _windowMonitor.StopMonitoring()),
                    Task.Run(() => _idleMonitor.StopMonitoring())
                );

                _windowMonitor.StopMonitoring();
                _idleMonitor.StopMonitoring() ;

                
                await _sessionManager.EndSessionAsync();

                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                Console.WriteLine("🛑 Window tracking stopped successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ StopTrackingAsync error: {ex.Message}");
                throw;
            }
        }
        #endregion

        //#region Event Handlers
        //private void OnWindowChanged(object sender, AppWindowChangedEventArgs e)
        //{
        //    Console.WriteLine($"🔹 Window changed at {DateTime.Now:HH:mm:ss}: {e.CurrentAppName} - {e.CurrentWindowTitle}");

        //    if (!_isTracking || _idleMonitor.IsIdle) return;

        //    FinalizeCurrentAppUsage();

        //    if (!string.IsNullOrEmpty(e.CurrentAppName))
        //    {
        //        _currentAppUsage = CreateAppUsage(e.CurrentAppName, e.CurrentWindowTitle);
        //        _lastSwitchTime = DateTime.Now;
        //        _sessionManager.AddAppUsage(_currentAppUsage);

        //        Console.WriteLine($"📊 Tracking new app: {e.CurrentAppName}, {e.CurrentWindowTitle}");
        //    }
        //}

        //private void OnIdleStateChanged(object sender, IdleStateChangedEventArgs e)
        //{
        //    if (!_isTracking) return;

        //    if (e.IsIdle)
        //    {
        //        FinalizeCurrentAppUsage();
        //        Console.WriteLine("⏸ User went idle, pausing tracking...");
        //    }
        //    else
        //    {
        //        var (appName, windowTitle) = _windowMonitor.GetActiveWindow();
        //        if (!string.IsNullOrEmpty(appName))
        //        {
        //            _currentAppUsage = CreateAppUsage(appName, windowTitle);
        //            _sessionManager.AddAppUsage(_currentAppUsage);
        //        }

        //        Console.WriteLine("▶️ User active again, resuming tracking...");
        //    }
        //}
         

        //#region Internal Event Subscriptions
        //private void HandleAppSwitchedInternal(object sender, AppUsage e)
        //{
        //    Console.WriteLine($"[Internal] App switched to: {e.AppName}");

        //    // Example: Update current session
        //    if (_sessionManager.CurrentSession != null)
        //    {
        //        _sessionManager.CurrentSession.LastUsedApp = e.AppName;
        //    }
        //}

        //private void HandleSessionCompletedInternal(object sender, List<AppUsage> appUsages)
        //{
        //    Console.WriteLine($"[Internal] Session completed. Total apps tracked: {appUsages.Count}");
        //}
        //#endregion

        //#region Core Logic
        //private AppUsage CreateAppUsage(string appName, string windowTitle)
        //{
        //    return new AppUsage
        //    {
        //        AppName = appName ?? "Unknown",
        //        WindowTitle = windowTitle ?? "Unknown",
        //        StartTime = DateTime.Now,
        //        IsProductive = _productivityClassifier.IsProductiveActivity(appName ?? "", windowTitle ?? "")
        //    };
        //}

        //private void FinalizeCurrentAppUsage()
        //{
        //    if (_currentAppUsage == null)
        //    {
        //        Console.WriteLine($"⚠️ No current app usage to finalize at {DateTime.Now:HH:mm:ss}");
        //        return;
        //    }

        //    _currentAppUsage.EndTime = DateTime.Now;
        //    _currentAppUsage.Duration = _currentAppUsage.EndTime - _lastSwitchTime;
        //    _currentAppUsage.IsProductive = _productivityClassifier.IsProductiveActivity(_currentAppUsage.AppName, _currentAppUsage.WindowTitle);

        //    if (_currentAppUsage.Duration.TotalSeconds >= 1)
        //    {
        //        // Raise event for external subscribers
        //        AppSwitched?.Invoke(this, _currentAppUsage);

        //        Console.WriteLine($"📢 AppSwitched event raised for: {_currentAppUsage.AppName}, Duration: {_currentAppUsage.Duration.TotalSeconds:F2}s");

        //        // Notify AI
        //        Task.Run(() => NotifyAiAsync(_currentAppUsage, _cts.Token));
        //    }

        //    _currentAppUsage = null;
        //}
        //#endregion

        //#region AI Notifications
        //private async Task NotifyAiAsync(AppUsage usage, CancellationToken cancellationToken)
        //{
        //    try
        //    {
        //        cancellationToken.ThrowIfCancellationRequested();
        //        if (usage == null) return;

        //        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        //        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

        //        var response = await _activityService.SendActivityAsync(usage).WithCancellation(linkedCts.Token);

        //        if (response.Status == "error" || string.IsNullOrEmpty(response.InterventionMessage))
        //        {
        //            Console.WriteLine("⚠️ AI is still learning from your data.");
        //            return;
        //        }

        //        await _activityManager.LogActivityAsync(usage);
        //        AiInterventionReceived?.Invoke(this, response);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        Console.WriteLine("⚠️ AI notification canceled or timed out.");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ AI notification failed: {ex.Message}");
        //    }
        //}
        //#endregion

        //#region Dispose
        //public async ValueTask DisposeAsync()
        //{
        //    try
        //    {
        //        if (_isTracking)
        //        {
        //            await StopTrackingAsync();
        //        }

        //        // Unsubscribe from events
        //        _windowMonitor.WindowChanged -= OnWindowChanged;
        //        _idleMonitor.IdleStateChanged -= OnIdleStateChanged;
        //        this.AppSwitched -= HandleAppSwitchedInternal;
        //        this.SessionCompleted -= HandleSessionCompletedInternal;

        //        _cts?.Dispose();
        //        Console.WriteLine("🗑 WindowTracker disposed.");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ DisposeAsync error: {ex.Message}");
        //    }
        //}

        //public void Dispose()
        //{
        //    DisposeAsync().GetAwaiter().GetResult();
        //}
        //#endregion
    }

    #region Task Extensions
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
    #endregion
}
