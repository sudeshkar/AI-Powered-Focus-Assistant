using FocusAssistant.Core.Focus;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Intelligence.Classification
{
    /// <summary>
    /// Classifies activity by comparing it to written descriptions of what people do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the layer that closes the hole in the keyword ruleset, which assumed any
    /// application it had never heard of was productive. It only ever sees inputs the rules
    /// could not decide, so it is not competing with them - it is answering the question
    /// they skipped.
    /// </para>
    /// <para>
    /// It abstains rather than guessing. Two conditions force silence: an absolute
    /// similarity floor, below which nothing resembles the input at all, and a margin
    /// between the best and second-best label, below which the input sits between two
    /// meanings. Both return null and let the caller fall through. A confident-sounding
    /// wrong answer here becomes a wrong interruption later, and that is the failure mode
    /// that gets focus apps uninstalled.
    /// </para>
    /// </remarks>
    public sealed class EmbeddingSemanticClassifier : ISemanticClassifier, IDisposable
    {
        private readonly Embeddings.MiniLmEmbeddingGenerator _embedder;
        private readonly ILogger<EmbeddingSemanticClassifier> _logger;
        private readonly double _minimumSimilarity;
        private readonly double _minimumMargin;

        private IReadOnlyList<LabelPrototype> _prototypes = [];
        private volatile bool _isReady;
        private bool _disposed;

        public bool IsReady => _isReady;

        public EmbeddingSemanticClassifier(
            Embeddings.MiniLmEmbeddingGenerator embedder,
            ILogger<EmbeddingSemanticClassifier> logger,
            double minimumSimilarity,
            double minimumMargin)
        {
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _minimumSimilarity = minimumSimilarity;
            _minimumMargin = minimumMargin;
        }

        /// <summary>
        /// Loads and embeds the prototypes. Roughly 30 short strings, so a few hundred
        /// milliseconds once, off the UI thread, at startup.
        /// </summary>
        public async Task WarmUpAsync(string labelFilePath, CancellationToken ct = default)
        {
            var json = await File.ReadAllTextAsync(labelFilePath, ct).ConfigureAwait(false);
            var file = JsonSerializer.Deserialize<LabelPrototypeFile>(json)
                ?? throw new InvalidDataException($"{labelFilePath} did not contain a label set.");

            foreach (var prototype in file.Labels)
                prototype.Embedding = await _embedder.EmbedAsync(prototype.Text, ct).ConfigureAwait(false);

            _prototypes = file.Labels;
            _isReady = true;

            _logger.LogInformation("Semantic classifier ready with {Count} prototypes", _prototypes.Count);
        }

        public async ValueTask<ProductivityVerdict?> ClassifyAsync(
            ActivityContext context, CancellationToken ct = default)
        {
            if (!_isReady || _prototypes.Count == 0)
                return null;

            try
            {
                var text = ActivityTextBuilder.Build(context.AppName, context.WindowTitle);
                var vector = await _embedder.EmbedAsync(text, ct).ConfigureAwait(false);

                LabelPrototype? best = null;
                double bestScore = double.NegativeInfinity;
                double runnerUpScore = double.NegativeInfinity;

                foreach (var prototype in _prototypes)
                {
                    if (prototype.Embedding is null)
                        continue;

                    var score = Embeddings.MiniLmEmbeddingGenerator.CosineSimilarity(vector, prototype.Embedding);
                    if (score > bestScore)
                    {
                        runnerUpScore = bestScore;
                        bestScore = score;
                        best = prototype;
                    }
                    else if (score > runnerUpScore)
                    {
                        runnerUpScore = score;
                    }
                }

                if (best is null || bestScore < _minimumSimilarity)
                    return null;

                // The margin is measured against the best prototype of the *opposite*
                // polarity, not simply the second best overall. Two productive prototypes
                // scoring 0.61 and 0.60 is not ambiguity - both agree - and treating it as
                // ambiguity would silence the classifier on exactly the inputs it
                // understands best.
                var opposing = BestOpposingScore(vector, best.IsProductive);
                if (bestScore - opposing < _minimumMargin)
                    return null;

                return new ProductivityVerdict(
                    IsProductive: best.IsProductive,
                    Confidence: Math.Clamp((bestScore - opposing) * 4, 0, 1),
                    Source: ClassificationSource.Embedding,
                    Rationale: $"closest match: {best.Text}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Classification is an enhancement. If the model misbehaves the app must
                // keep tracking on the ruleset, not stop working.
                _logger.LogWarning(ex, "Semantic classification failed for {App}", context.AppName);
                return null;
            }
        }

        private double BestOpposingScore(float[] vector, bool isProductive)
        {
            double best = double.NegativeInfinity;
            foreach (var prototype in _prototypes)
            {
                if (prototype.Embedding is null || prototype.IsProductive == isProductive)
                    continue;

                var score = Embeddings.MiniLmEmbeddingGenerator.CosineSimilarity(vector, prototype.Embedding);
                if (score > best)
                    best = score;
            }

            return double.IsNegativeInfinity(best) ? 0 : best;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _embedder.Dispose();
        }
    }
}
