using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Models;
using FocusAssistant.Core.Monitoring;
using FocusAssistant.Core.Session;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace FocusAssistant.ViewModels
{
    /// <summary>
    /// Backs the Tracking view: what is happening right now, and the goal for this stretch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to offer Start Tracking and Stop Tracking buttons. Since tracking became a
    /// background service that runs for the life of the process (see
    /// <c>TrackingHostedService</c>), those buttons were actively misleading: Start was
    /// disabled because tracking was already running, which read as broken rather than as
    /// "already on". What the user can actually decide here is what they are working
    /// <i>on</i>, so the control is a goal, not a switch.
    /// </para>
    /// <para>
    /// Setting a goal does not restart tracking - the monitors keep running - it starts a
    /// fresh session under <see cref="ISessionEngine"/> so today's totals split at the
    /// point the goal changed, and so <see cref="IGoalRelevanceScorer"/> has something to
    /// compare activity against.
    /// </para>
    /// <para>
    /// The activity log used to subscribe to <see cref="IWindowMonitor.WindowChanged"/>
    /// directly, which meant every row read "in progress" forever - nothing ever updated
    /// it - and showed alt-tab noise the two-second minimum-duration filter exists to
    /// discard. It now subscribes to <see cref="ISessionEngine.ActivityRecorded"/>, so a
    /// row appears once with its real, final duration and its classified verdict.
    /// </para>
    /// </remarks>
    public partial class TrackingViewModel : ObservableObject, IDisposable
    {
        private const int MaxActivityLogEntries = 200;

        private readonly IWindowMonitor _windowMonitor;
        private readonly ISessionEngine _sessionEngine;
        private readonly IActivityClassifier _classifier;
        private readonly IProductivityStrategy _categories;
        private bool _disposed;

        [ObservableProperty]
        private string _goal = string.Empty;

        [ObservableProperty]
        private string _activeGoal = string.Empty;

        [ObservableProperty]
        private bool _hasActiveGoal;

        [ObservableProperty]
        private string _currentApp = "No application detected";

        [ObservableProperty]
        private string _currentWindow = string.Empty;

        /// <summary>Why the current app/window was classified the way it was.</summary>
        [ObservableProperty]
        private string _currentRationale = string.Empty;

        [ObservableProperty]
        private bool _isCurrentProductive = true;

        public ObservableCollection<ActivityLogItem> ActivityLog { get; } = [];

        public TrackingViewModel(
            IWindowMonitor windowMonitor,
            ISessionEngine sessionEngine,
            IActivityClassifier classifier,
            IProductivityStrategy categories)
        {
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _sessionEngine = sessionEngine ?? throw new ArgumentNullException(nameof(sessionEngine));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));

            _windowMonitor.WindowChanged += OnWindowChanged;
            _sessionEngine.ActivityRecorded += OnActivityRecorded;

            ActiveGoal = _sessionEngine.CurrentGoal ?? string.Empty;
            HasActiveGoal = !string.IsNullOrEmpty(ActiveGoal);

            var (app, title) = _windowMonitor.GetActiveWindow();
            if (!string.IsNullOrEmpty(app))
                UpdateCurrentActivity(app, title);
        }

        [RelayCommand]
        private async Task StartFocusSessionAsync()
        {
            var trimmed = Goal.Trim();

            // The monitors are already running (TrackingHostedService owns that); this
            // only closes today's totals at this moment and opens a fresh session so
            // they split around the new goal.
            await _sessionEngine.StartSessionAsync(string.IsNullOrWhiteSpace(trimmed) ? null : trimmed);

            ActiveGoal = _sessionEngine.CurrentGoal ?? string.Empty;
            HasActiveGoal = !string.IsNullOrEmpty(ActiveGoal);
            Goal = string.Empty;
        }

        [RelayCommand]
        private async Task ClearGoalAsync()
        {
            await _sessionEngine.StartSessionAsync(null);
            ActiveGoal = string.Empty;
            HasActiveGoal = false;
        }

        private void OnWindowChanged(object? sender, AppWindowChangedEventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            dispatcher?.InvokeAsync(() => UpdateCurrentActivity(e.CurrentAppName, e.CurrentWindowTitle));
        }

        /// <summary>
        /// Classifies the foreground app/window for display, using the same fast path the
        /// session engine uses, so what the user sees here matches what gets recorded.
        /// </summary>
        private void UpdateCurrentActivity(string appName, string? windowTitle)
        {
            CurrentApp = appName;
            CurrentWindow = windowTitle ?? string.Empty;

            var context = new ActivityContext(
                appName, windowTitle, _categories.GetCategory(appName), DateTimeOffset.Now, ActiveGoal);
            var verdict = _classifier.ClassifyFast(context);

            IsCurrentProductive = verdict.IsProductive;
            CurrentRationale = verdict.Source == ClassificationSource.Default
                ? "not sure yet - refining in the background"
                : verdict.Rationale ?? string.Empty;
        }

        /// <summary>
        /// A completed, classified stretch. Raised off the session lock, so this is safe to
        /// marshal straight to the dispatcher.
        /// </summary>
        private void OnActivityRecorded(object? sender, AppUsage usage)
        {
            var dispatcher = Application.Current?.Dispatcher;
            dispatcher?.InvokeAsync(() =>
            {
                ActivityLog.Insert(0, new ActivityLogItem
                {
                    AppName = usage.AppName,
                    WindowTitle = usage.WindowTitle,
                    TimeText = usage.StartTime.ToString("HH:mm:ss"),
                    DurationText = FormatDuration(usage.Duration),
                    IsProductive = usage.IsProductive,
                });

                while (ActivityLog.Count > MaxActivityLogEntries)
                    ActivityLog.RemoveAt(ActivityLog.Count - 1);
            });
        }

        private static string FormatDuration(TimeSpan span) =>
            span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m {span.Seconds}s" : $"{span.Seconds}s";

        public void ClearActivityLog() => ActivityLog.Clear();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Both are singletons; without this the view model stays reachable from
            // them for the life of the process.
            _windowMonitor.WindowChanged -= OnWindowChanged;
            _sessionEngine.ActivityRecorded -= OnActivityRecorded;
        }
    }
}
