using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Classifies activity by meaning rather than by keyword. Implemented in the
    /// Intelligence project - Core never learns what a tensor is.
    /// </summary>
    public interface ISemanticClassifier
    {
        /// <summary>False until the model has loaded. Callers must cope with both.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Loads whatever the implementation needs before it can classify. Part of this
        /// interface rather than a separate one so the startup path never has to know
        /// whether it is holding a real classifier or the abstaining stand-in.
        /// </summary>
        Task WarmUpAsync(string labelFilePath, CancellationToken ct = default);

        /// <summary>
        /// Returns null when the model is not loaded, or when it is not confident enough to
        /// speak. Abstaining is a real answer: a confident-sounding wrong verdict is worse
        /// than deferring to the layer below.
        /// </summary>
        ValueTask<ProductivityVerdict?> ClassifyAsync(ActivityContext context, CancellationToken ct = default);
    }
}
