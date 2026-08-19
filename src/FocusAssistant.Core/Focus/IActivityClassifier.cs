using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// The composed classifier the session engine consumes.
    /// </summary>
    /// <remarks>
    /// Two methods rather than one, because the two callers have opposite needs. The
    /// session engine classifies inside a lock on a polling thread and cannot await
    /// anything; the refinement service runs afterwards on its own thread and can afford
    /// the model. A single async method would have meant either blocking the hot path or
    /// never using the model at all.
    /// </remarks>
    public interface IActivityClassifier
    {
        /// <summary>
        /// Never blocks and never runs a model: overrides, rules, and cached results only.
        /// </summary>
        ProductivityVerdict ClassifyFast(ActivityContext context);

        /// <summary>
        /// May run the embedding model. Off the hot path, and warms the cache that
        /// <see cref="ClassifyFast"/> reads - the same application and title recur
        /// constantly, which is what makes the fast path accurate in practice rather than
        /// only in theory.
        /// </summary>
        ValueTask<ProductivityVerdict> ClassifyAsync(ActivityContext context, CancellationToken ct = default);
    }
}
