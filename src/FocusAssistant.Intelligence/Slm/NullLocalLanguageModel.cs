using FocusAssistant.Core.Intelligence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Intelligence.Slm
{
    /// <summary>
    /// A language model that never has anything to say.
    /// </summary>
    /// <remarks>
    /// Registered whenever the real model is disabled, so no consumer ever holds a null
    /// <see cref="ILocalLanguageModel"/>. Returning null from a live object is already the
    /// documented normal case, so callers need no extra branch for "there is no model at
    /// all" - it is the same code path as "the model declined to answer".
    /// </remarks>
    public sealed class NullLocalLanguageModel : ILocalLanguageModel
    {
        public ModelAvailability Availability => ModelAvailability.Disabled;

        public event EventHandler<ModelAvailability>? AvailabilityChanged
        {
            add { }
            remove { }
        }

        public Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public async IAsyncEnumerable<string> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
