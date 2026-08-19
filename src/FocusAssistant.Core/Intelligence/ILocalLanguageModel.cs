using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Intelligence
{
    /// <summary>
    /// A small language model running on this machine.
    /// </summary>
    /// <remarks>
    /// Every caller must treat this as optional. The model is a 2.5GB download the user may
    /// never make, may delete, or may be unable to load - so <see cref="GenerateAsync"/>
    /// returns null rather than throwing, and every feature built on it needs a version of
    /// itself that reads well without it. Nothing in the product may become unavailable
    /// because a model is missing.
    /// </remarks>
    public interface ILocalLanguageModel
    {
        ModelAvailability Availability { get; }

        event EventHandler<ModelAvailability>? AvailabilityChanged;

        /// <summary>
        /// Generates a single response, or null when the model is unavailable, times out,
        /// or fails. Callers must handle null - it is the normal case, not an error.
        /// </summary>
        Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default);

        /// <summary>
        /// Streams a response token by token, for the places where watching text appear
        /// beats waiting ten seconds for a paragraph. Yields nothing when unavailable.
        /// </summary>
        IAsyncEnumerable<string> StreamAsync(LlmRequest request, CancellationToken ct = default);
    }
}
