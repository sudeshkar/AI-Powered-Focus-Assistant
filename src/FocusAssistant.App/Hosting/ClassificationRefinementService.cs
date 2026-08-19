using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Models;
using FocusAssistant.Core.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Re-classifies completed activity with the embedding model, off the hot path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The session engine has to decide productive-or-not synchronously, inside a lock, on
    /// a polling thread - so it uses the fast path, which never runs a model. This service
    /// picks the same activity up afterwards and asks the slow path, which does.
    /// </para>
    /// <para>
    /// The result is written back onto the usage record, and - more importantly - into the
    /// classifier's cache. People return to the same few windows all day, so the second
    /// time an application is seen the fast path already has the model's answer for it.
    /// That is what makes a non-blocking hot path accurate in practice rather than merely
    /// in principle.
    /// </para>
    /// <para>
    /// The channel is bounded and drops the oldest item when full. If classification ever
    /// falls behind the user's window switching, the right failure is to lose some
    /// refinements, not to grow a queue without limit in a process that runs all day.
    /// </para>
    /// </remarks>
    public sealed class ClassificationRefinementService : IHostedService, IDisposable
    {
        private const int QueueCapacity = 256;

        private readonly ISessionEngine _sessionEngine;
        private readonly IActivityClassifier _classifier;
        private readonly IProductivityStrategy _categories;
        private readonly ILogger<ClassificationRefinementService> _logger;

        private readonly Channel<AppUsage> _queue = Channel.CreateBounded<AppUsage>(
            new BoundedChannelOptions(QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });

        private readonly CancellationTokenSource _stopping = new();
        private Task? _worker;
        private bool _disposed;

        public ClassificationRefinementService(
            ISessionEngine sessionEngine,
            IActivityClassifier classifier,
            IProductivityStrategy categories,
            ILogger<ClassificationRefinementService> logger)
        {
            _sessionEngine = sessionEngine ?? throw new ArgumentNullException(nameof(sessionEngine));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _sessionEngine.ActivityRecorded += OnActivityRecorded;
            _worker = Task.Run(() => ProcessAsync(_stopping.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _sessionEngine.ActivityRecorded -= OnActivityRecorded;
            _queue.Writer.TryComplete();
            await _stopping.CancelAsync().ConfigureAwait(false);

            if (_worker is not null)
            {
                // Swallow the cancellation this just caused; anything else is worth seeing.
                try { await _worker.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }

        /// <summary>
        /// Raised on the window-poll thread. Must not block - it only hands the item over.
        /// </summary>
        private void OnActivityRecorded(object? sender, AppUsage usage) => _queue.Writer.TryWrite(usage);

        private async Task ProcessAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var usage in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        var context = new ActivityContext(
                            usage.AppName,
                            usage.WindowTitle,
                            _categories.GetCategory(usage.AppName),
                            usage.StartTime,
                            _sessionEngine.CurrentGoal);

                        var verdict = await _classifier.ClassifyAsync(context, ct).ConfigureAwait(false);

                        if (verdict.IsProductive != usage.IsProductive)
                        {
                            _logger.LogDebug(
                                "Reclassified {App} as {Verdict} ({Source}, {Confidence:F2})",
                                usage.AppName,
                                verdict.IsProductive ? "productive" : "distracting",
                                verdict.Source,
                                verdict.Confidence);

                            usage.IsProductive = verdict.IsProductive;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // One bad item must not end the loop; tracking would silently stop
                        // being refined for the rest of the session.
                        _logger.LogWarning(ex, "Could not refine classification for {App}", usage.AppName);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown.
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _stopping.Dispose();
        }
    }
}
