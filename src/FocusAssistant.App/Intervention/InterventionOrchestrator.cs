using FocusAssistant.Core.Data.Abstractions;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Intervention;
using FocusAssistant.Core.Models;
using FocusAssistant.Core.Monitoring;
using FocusAssistant.Core.Session;
using FocusAssistant.Data.Stores;
using FocusAssistant.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Intervention
{
    /// <summary>
    /// The only subscriber to live activity that decides whether to nudge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs on its own timer rather than only reacting to window switches, because the
    /// interesting case - someone has sat in one distracting window for ten minutes - never
    /// raises a switch event. Every tick classifies the current foreground window, feeds it
    /// to the detector, and asks the policy whether that is worth a nudge.
    /// </para>
    /// <para>
    /// This is deliberately not on <see cref="ISessionEngine.ActivityRecorded"/>: that event
    /// fires when a stretch <i>ends</i>, which is too late to interrupt it. Classification
    /// still runs through <see cref="IActivityClassifier.ClassifyFast"/>, the same
    /// non-blocking path the session engine uses, so what triggers a nudge is exactly what
    /// gets recorded.
    /// </para>
    /// </remarks>
    public sealed class InterventionOrchestrator : IHostedService, IDisposable
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

        private readonly IWindowMonitor _windowMonitor;
        private readonly ISessionEngine _sessionEngine;
        private readonly IActivityClassifier _classifier;
        private readonly IProductivityStrategy _categories;
        private readonly IDistractionDetector _detector;
        private readonly IInterventionPolicy _policy;
        private readonly IInterventionDispatcher _dispatcher;
        private readonly SqliteUserOverrideStore _overrides;
        private readonly IBaseService<InterventionOutcome> _outcomes;
        private readonly StartupState _startupState;
        private readonly ILogger<InterventionOrchestrator> _logger;

        private Timer? _timer;
        private volatile bool _showing;
        private bool _disposed;

        public InterventionOrchestrator(
            IWindowMonitor windowMonitor,
            ISessionEngine sessionEngine,
            IActivityClassifier classifier,
            IProductivityStrategy categories,
            IDistractionDetector detector,
            IInterventionPolicy policy,
            IInterventionDispatcher dispatcher,
            SqliteUserOverrideStore overrides,
            IBaseService<InterventionOutcome> outcomes,
            StartupState startupState,
            ILogger<InterventionOrchestrator> logger)
        {
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));
            _sessionEngine = sessionEngine ?? throw new ArgumentNullException(nameof(sessionEngine));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
            _outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // The timer itself only reads live process state (no database) until a
            // suggestion actually fires, but WarmUpAsync queries UserOverrides directly and
            // migrations may not have run yet - the same "schema does not exist on a fresh
            // run" race every other DB-touching startup path in this app has to guard
            // against. Waiting here, rather than starting the timer immediately, keeps that
            // guard in one place instead of pushing it onto every tick.
            _ = Task.Run(async () =>
            {
                if (await _startupState.DatabaseReady.ConfigureAwait(false))
                    await _overrides.WarmUpAsync().ConfigureAwait(false);
            }, CancellationToken.None);

            _timer = new Timer(_ => _ = TickAsync(), null, TickInterval, TickInterval);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        private async Task TickAsync()
        {
            // A nudge is already showing; do not evaluate again until the user has answered
            // it. Stacking suggestions is exactly the behaviour the cadence limits exist to
            // prevent, and this is the one path they do not otherwise cover.
            if (_showing || !_sessionEngine.IsSessionActive)
                return;

            try
            {
                var (appName, windowTitle) = _windowMonitor.GetActiveWindow();
                if (string.IsNullOrEmpty(appName))
                    return;

                var now = DateTimeOffset.Now;
                var context = new ActivityContext(
                    appName, windowTitle, _categories.GetCategory(appName), now, _sessionEngine.CurrentGoal);

                var verdict = _classifier.ClassifyFast(context);
                var signal = _detector.Observe(context, verdict, now);
                if (signal is null)
                    return;

                var suggestion = _policy.Decide(signal, now);
                if (suggestion is null)
                    return;

                suggestion = suggestion with { ReturnApp = _detector.LastProductiveApp };

                await ShowAndRecordAsync(suggestion, signal, now);
            }
            catch (Exception ex)
            {
                // A failed tick must not take the timer down - the next one nine seconds
                // later should still run.
                _logger.LogError(ex, "Intervention tick failed");
            }
        }

        private async Task ShowAndRecordAsync(InterventionSuggestion suggestion, DistractionSignal signal, DateTimeOffset shownAt)
        {
            _showing = true;
            try
            {
                var response = await _dispatcher.ShowAsync(suggestion);
                var respondedAt = DateTimeOffset.Now;

                _policy.RecordResponse(suggestion, response, respondedAt);

                if (response == InterventionResponse.DismissedPolitely)
                    await _overrides.SetOverrideAsync(suggestion.AppName, isProductive: true);

                var returnedToWork = await ReturnedToProductiveWorkAsync();

                var outcome = new InterventionOutcome
                {
                    wID = _sessionEngine.CurrentWorkSessionId ?? string.Empty,
                    ShownAt = shownAt.LocalDateTime,
                    AppName = suggestion.AppName,
                    TriggerRationale = suggestion.Rationale ?? string.Empty,
                    Tier = suggestion.Tier,
                    DistractionRisk = suggestion.DistractionRisk,
                    Response = response,
                    TimeToRespond = respondedAt - shownAt,
                    ReturnedToWork = returnedToWork,
                };

                await _outcomes.CreateAsync(outcome);
            }
            finally
            {
                _showing = false;
            }
        }

        /// <summary>
        /// Whether a productive app is in the foreground a short while after the nudge,
        /// measured rather than inferred from which button was clicked - clicking "Back to
        /// VS Code" proves intent, not that it happened. This is a single sample rather than
        /// a full two-minute watch window: good enough to tell an ignored nudge from one that
        /// worked without holding the orchestrator's one nudge-at-a-time slot open for
        /// minutes at a time.
        /// </summary>
        private async Task<bool> ReturnedToProductiveWorkAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(20));

            var (appName, windowTitle) = _windowMonitor.GetActiveWindow();
            if (string.IsNullOrEmpty(appName))
                return false;

            var context = new ActivityContext(
                appName, windowTitle, _categories.GetCategory(appName), DateTimeOffset.Now, _sessionEngine.CurrentGoal);
            return _classifier.ClassifyFast(context).IsProductive;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer?.Dispose();
        }
    }
}
