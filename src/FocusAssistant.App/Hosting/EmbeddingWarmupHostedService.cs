using FocusAssistant.Core.Focus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Loads the embedding model and embeds the label prototypes, off the UI thread.
    /// </summary>
    /// <remarks>
    /// A few hundred milliseconds, which is short enough to be tempting to do inline and
    /// long enough to be visible as a stutter on the first frame. Until it finishes the
    /// layered classifier simply falls through to the keyword ruleset, which is the same
    /// behaviour the app had before this model existed - so nothing has to wait for it.
    /// </remarks>
    public sealed class EmbeddingWarmupHostedService : IHostedService
    {
        private readonly ISemanticClassifier _classifier;
        private readonly StartupState _startupState;
        private readonly ILogger<EmbeddingWarmupHostedService> _logger;

        public EmbeddingWarmupHostedService(
            ISemanticClassifier classifier,
            StartupState startupState,
            ILogger<EmbeddingWarmupHostedService> logger)
        {
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(() => WarmUpAsync(cancellationToken), CancellationToken.None);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task WarmUpAsync(CancellationToken cancellationToken)
        {
            try
            {
                var labels = Path.Combine(AppContext.BaseDirectory, "Assets", "focus_labels.json");

                var sw = Stopwatch.StartNew();
                await _classifier.WarmUpAsync(labels, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                _startupState.IsEmbeddingReady = true;
                _logger.LogInformation("Embedding classifier warmed up in {Elapsed} ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // Not fatal, and not even worth a banner: without the model the app
                // classifies exactly as it did before, using keywords.
                _logger.LogError(ex, "Embedding warm-up failed; falling back to keyword rules");
            }
        }
    }
}
