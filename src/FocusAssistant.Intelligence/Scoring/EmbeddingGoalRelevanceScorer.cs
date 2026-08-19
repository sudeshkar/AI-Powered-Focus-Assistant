using FocusAssistant.Core.Focus;
using FocusAssistant.Intelligence.Classification;
using FocusAssistant.Intelligence.Embeddings;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Intelligence.Scoring
{
    /// <summary>
    /// Scores activity against the goal the user typed when they started the session.
    /// </summary>
    /// <remarks>
    /// The weakest layer, and deliberately last: a goal is one short phrase, so similarity
    /// to it is noisy compared to a curated prototype. What it adds is the only knowledge
    /// in the system about <i>this particular session</i> - the same documentation site is
    /// on-task while writing docs and off-task while fixing a build, and nothing above this
    /// layer can tell those apart.
    /// </remarks>
    public sealed class EmbeddingGoalRelevanceScorer : IGoalRelevanceScorer
    {
        private readonly MiniLmEmbeddingGenerator _embedder;
        private readonly ILogger<EmbeddingGoalRelevanceScorer> _logger;

        // Written on the UI thread when a session starts, read on the classification
        // thread. Reference assignment is atomic and a stale read for one activity is
        // harmless, so this needs no lock.
        private volatile float[]? _goalEmbedding;

        public EmbeddingGoalRelevanceScorer(
            MiniLmEmbeddingGenerator embedder,
            ILogger<EmbeddingGoalRelevanceScorer> logger)
        {
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SetGoalAsync(string? goal, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(goal))
            {
                _goalEmbedding = null;
                return;
            }

            try
            {
                // Embedded once per session rather than per activity: the goal does not
                // change while the session runs.
                _goalEmbedding = await _embedder.EmbedAsync(goal.Trim(), ct).ConfigureAwait(false);
                _logger.LogInformation("Goal relevance scoring enabled for this session");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not embed the session goal; relevance scoring is off");
                _goalEmbedding = null;
            }
        }

        public async ValueTask<double?> ScoreAsync(ActivityContext context, CancellationToken ct = default)
        {
            var goal = _goalEmbedding;
            if (goal is null)
                return null;

            try
            {
                var text = ActivityTextBuilder.Build(context.AppName, context.WindowTitle);
                var vector = await _embedder.EmbedAsync(text, ct).ConfigureAwait(false);

                // Cosine runs -1..1; the caller wants 0..1 where 0.5 means "no signal".
                var cosine = MiniLmEmbeddingGenerator.CosineSimilarity(vector, goal);
                return Math.Clamp((cosine + 1) / 2, 0, 1);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Goal relevance scoring failed for {App}", context.AppName);
                return null;
            }
        }
    }
}
