using FocusAssistant.Core.Focus;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Intelligence.Classification
{
    /// <summary>
    /// A classifier that always abstains, used when the embedding model is disabled or
    /// failed to load.
    /// </summary>
    /// <remarks>
    /// Registered in place of the real one rather than leaving the dependency null, so no
    /// consumer ever has to null-check an <see cref="ISemanticClassifier"/>. Abstention is
    /// already a supported answer everywhere, so a missing model degrades to exactly the
    /// keyword behaviour the app had before - with no special-casing anywhere.
    /// </remarks>
    public sealed class NullSemanticClassifier : ISemanticClassifier
    {
        public bool IsReady => false;

        public Task WarmUpAsync(string labelFilePath, CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask<ProductivityVerdict?> ClassifyAsync(
            ActivityContext context, CancellationToken ct = default) => new((ProductivityVerdict?)null);
    }

    /// <summary>A goal scorer that never has an opinion. See <see cref="NullSemanticClassifier"/>.</summary>
    public sealed class NullGoalRelevanceScorer : IGoalRelevanceScorer
    {
        public Task SetGoalAsync(string? goal, CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask<double?> ScoreAsync(ActivityContext context, CancellationToken ct = default) =>
            new((double?)null);
    }
}
