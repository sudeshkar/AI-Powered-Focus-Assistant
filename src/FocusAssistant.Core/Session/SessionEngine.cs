using FocusAssistant.Core.Data.Abstractions;
using Microsoft.Extensions.Logging;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Models;
using FocusAssistant.Core.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
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
    /// <para>
    /// Usage rows are written as they complete, not in one batch when the session ends.
    /// The batch-at-the-end version kept an entire day in memory and committed nothing
    /// until a clean shutdown, so a crash, a power cut, or Task Manager destroyed every
    /// minute of it. That was survivable only because the window closing ended the
    /// session; once tracking outlives the window it is the most damaging bug in the
    /// codebase. Writes are batched on a short timer rather than issued per row - one
    /// SaveChanges per application switch would be a disk write every few seconds all
    /// day - which bounds the worst case to the flush interval rather than to the day.
    /// </para>
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
        private readonly IActivityClassifier _classifier;
        private readonly ILogger<SessionEngine> _logger;

        /// <summary>
        /// How long a completed usage may sit unwritten. This is the exact size of the
        /// window a crash can destroy, so it is deliberately short; the batching exists to
        /// avoid a write per application switch, not to defer work indefinitely.
        /// </summary>
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

        /// <summary>Flush early once this many rows are waiting, however recent they are.</summary>
        private const int FlushBatchSize = 20;

        private readonly object _sessionLock = new();

        /// <summary>
        /// Completed usages waiting to be written. Unbounded on purpose: dropping tracked
        /// activity to protect memory would be trading the bug being fixed for a quieter
        /// version of itself, and the writer drains far faster than a person switches
        /// windows.
        /// </summary>
        private readonly Channel<AppUsage> _pendingWrites = Channel.CreateUnbounded<AppUsage>(
            new UnboundedChannelOptions { SingleReader = true });

        private readonly CancellationTokenSource _shutdown = new();
        private readonly Task _writerTask;

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
            IProductivityStrategy productivityStrategy,
            IActivityClassifier classifier,
            ILogger<SessionEngine> logger)
        {
            _userSessions = userSessions ?? throw new ArgumentNullException(nameof(userSessions));
            _workSessions = workSessions ?? throw new ArgumentNullException(nameof(workSessions));
            _appUsages = appUsages ?? throw new ArgumentNullException(nameof(appUsages));
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _idleMonitor = idleMonitor ?? throw new ArgumentNullException(nameof(idleMonitor));
            _productivityStrategy = productivityStrategy ?? throw new ArgumentNullException(nameof(productivityStrategy));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _windowMonitor.WindowChanged += OnWindowChanged;
            _idleMonitor.IdleStateChanged += OnIdleStateChanged;

            // Runs for the lifetime of the engine rather than per session, so a usage
            // queued as a session closes cannot be stranded by the writer stopping first.
            _writerTask = Task.Run(() => WriteQueuedUsagesAsync(_shutdown.Token), CancellationToken.None);
        }

        /// <summary>
        /// Drains completed usages to the database, batching by size and by time.
        /// </summary>
        private async Task WriteQueuedUsagesAsync(CancellationToken ct)
        {
            var batch = new List<AppUsage>(FlushBatchSize);

            try
            {
                while (await _pendingWrites.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    // Take everything already queued, then wait out the flush interval to
                    // let a burst of switches coalesce into one round trip.
                    while (batch.Count < FlushBatchSize && _pendingWrites.Reader.TryRead(out var queued))
                        batch.Add(queued);

                    if (batch.Count < FlushBatchSize)
                    {
                        try
                        {
                            await Task.Delay(FlushInterval, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // Shutting down: write what is in hand rather than losing it.
                        }

                        while (batch.Count < FlushBatchSize && _pendingWrites.Reader.TryRead(out var queued))
                            batch.Add(queued);
                    }

                    await FlushAsync(batch).ConfigureAwait(false);
                    batch.Clear();

                    if (ct.IsCancellationRequested)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
            finally
            {
                // Anything queued between the last read and cancellation still belongs on
                // disk - this is the path that runs when the app is closing.
                while (_pendingWrites.Reader.TryRead(out var queued))
                    batch.Add(queued);

                if (batch.Count > 0)
                    await FlushAsync(batch).ConfigureAwait(false);
            }
        }

        private async Task FlushAsync(List<AppUsage> batch)
        {
            if (batch.Count == 0)
                return;

            try
            {
                await _appUsages.CreateRangeAsync(batch).ConfigureAwait(false);
                _logger.LogDebug("Persisted {Count} app usage(s)", batch.Count);
            }
            catch (Exception ex)
            {
                // Losing a batch is bad but not worth stopping tracking over; the next one
                // may well succeed, and the alternative is losing everything after it too.
                _logger.LogError(ex, "Failed to persist {Count} app usage(s)", batch.Count);
            }
        }

        public async Task StartSessionAsync(string? goal = null)
        {
            if (IsSessionActive)
            {
                _logger.LogInformation("Session already active; ending it before starting a new one");
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

            _logger.LogInformation("Session {SessionId} started (goal: {Goal})",
                userSession.SessionId, CurrentGoal ?? "none");
            SessionStarted?.Invoke(this, userSession);
        }

        public async Task EndSessionAsync()
        {
            UserSession? userSession;
            WorkSession? workSession;

            lock (_sessionLock)
            {
                if (_currentUserSession is null || _currentWorkSession is null)
                    return;

                userSession = _currentUserSession;
                workSession = _currentWorkSession;

                // The session is ending; subscribers are told via SessionEnded instead.
                _ = CloseCurrentAppUsage(DateTime.Now);

                workSession.EndTime = DateTime.Now;
                workSession.Duration = workSession.EndTime - workSession.StartTime;
                workSession.CalculateStatistics();
                workSession.Status = WorkSessionStatus.Completed;

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
                // The usages are already on disk, or on their way there; wait for the queue
                // to clear so the session totals are not written before the rows they
                // describe.
                await DrainPendingWritesAsync().ConfigureAwait(false);

                await _workSessions.UpdateAsync(workSession);
                await _userSessions.UpdateAsync(userSession);
                _logger.LogInformation("Session {SessionId} closed with {Count} app usages",
                    userSession.SessionId, workSession.AppUsages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save session");
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
                _logger.LogWarning(ex, "Could not load today's sessions");
                return new List<WorkSession>();
            }
        }

        private void OnIdleStateChanged(object? sender, IdleStateChangedEventArgs e)
        {
            AppUsage? completed = null;

            lock (_sessionLock)
            {
                if (_currentWorkSession is null)
                    return;

                if (e.IsIdle)
                {
                    // Idle time is not work time: close the current usage so the
                    // duration does not absorb the whole break.
                    completed = CloseCurrentAppUsage(e.ChangeTime);
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

            RaiseActivityRecorded(completed);
        }

        private void OnWindowChanged(object? sender, AppWindowChangedEventArgs e)
        {
            // Invoked from a timer thread. Nothing may escape, or an unhandled
            // exception on a threadpool thread takes the process down.
            try
            {
                AppUsage? completed;
                lock (_sessionLock)
                {
                    if (_currentWorkSession is null)
                        return;

                    completed = CloseCurrentAppUsage(e.ChangeTime);
                    BeginAppUsageCore(e.CurrentAppName, e.CurrentWindowTitle, e.ChangeTime);
                }

                RaiseActivityRecorded(completed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error handling window change");
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
                // ClassifyFast, never ClassifyAsync: this runs inside the session lock on
                // the window-poll thread. The refinement service revisits the same activity
                // off the hot path and lets the model correct this.
                IsProductive = _classifier.ClassifyFast(new ActivityContext(
                    appName,
                    windowTitle,
                    _productivityStrategy.GetCategory(appName),
                    startTime,
                    CurrentGoal)).IsProductive,
            };
        }

        /// <summary>
        /// Closes the current stretch. Caller must hold the session lock.
        /// </summary>
        /// <returns>
        /// The completed usage, for the caller to announce <i>after</i> releasing the lock,
        /// or null when the stretch was too short to record.
        /// </returns>
        /// <remarks>
        /// This used to raise ActivityRecorded itself, from inside the lock, with a comment
        /// requiring every subscriber to stay fast. That constraint is not enforceable: the
        /// intervention pipeline is the first real subscriber, it runs a model, and a
        /// subscriber that awaits while holding this lock stalls the window-poll thread and
        /// freezes tracking. Returning the usage instead moves the decision to the one place
        /// that knows when the lock is free.
        /// </remarks>
        private AppUsage? CloseCurrentAppUsage(DateTime endTime)
        {
            if (_currentAppUsage is null || _currentWorkSession is null)
                return null;

            var usage = _currentAppUsage;
            _currentAppUsage = null;

            usage.EndTime = endTime;
            usage.Duration = endTime - usage.StartTime;

            if (usage.Duration < MinimumUsageDuration)
                return null;

            _currentWorkSession.AppUsages.Add(usage);

            // Queued rather than written here: this runs under the session lock on the
            // window-poll thread, and a database round trip on that path would stall
            // tracking for as long as the disk took.
            _pendingWrites.Writer.TryWrite(usage);

            return usage;
        }

        /// <summary>
        /// Announces a completed stretch. Must be called with the session lock released.
        /// </summary>
        private void RaiseActivityRecorded(AppUsage? usage)
        {
            if (usage is null)
                return;

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

        /// <summary>
        /// Waits for the write queue to empty, with a ceiling so a database that has stopped
        /// responding cannot hang shutdown.
        /// </summary>
        private async Task DrainPendingWritesAsync()
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

            while (_pendingWrites.Reader.Count > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(50).ConfigureAwait(false);

            if (_pendingWrites.Reader.Count > 0)
                _logger.LogWarning("{Count} app usage(s) still unwritten after draining",
                    _pendingWrites.Reader.Count);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _windowMonitor.WindowChanged -= OnWindowChanged;
            _idleMonitor.IdleStateChanged -= OnIdleStateChanged;

            // Let the writer's finally block flush what is still queued before the process
            // goes away. Bounded, because Dispose must not hang on a stuck disk.
            _pendingWrites.Writer.TryComplete();
            _shutdown.Cancel();

            try
            {
                _writerTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Cancellation, which is how this is expected to end.
            }

            _shutdown.Dispose();
        }
    }
}
