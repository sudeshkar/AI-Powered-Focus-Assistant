using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Intelligence
{
    /// <summary>
    /// Gets the language model onto the machine, and off it again.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ILocalLanguageModel"/> because downloading and running are
    /// different concerns with different failure modes and very different durations - and
    /// because the delete half matters as much as the download half. A 2.5GB file the user
    /// cannot remove from inside the app is a bad citizen on their disk.
    /// </remarks>
    public interface IModelProvisioner
    {
        ModelAvailability Status { get; }

        /// <summary>Total download size, for telling the user before they commit to it.</summary>
        long EstimatedBytes { get; }

        /// <summary>True when every file is present and the right size.</summary>
        bool IsDownloaded { get; }

        /// <summary>
        /// Downloads anything missing. Resumable, cancellable, and idempotent: interrupting
        /// it and calling it again continues rather than restarting.
        /// </summary>
        Task<bool> EnsureDownloadedAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken ct = default);

        Task DeleteAsync(CancellationToken ct = default);
    }
}
