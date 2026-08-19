using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// What the shell binds to while the slow parts of startup are still running, and
    /// how everything that needs the database waits for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is deliberately no splash window. The real window appearing instantly in a
    /// warming state is a better experience than a splash hiding an app that may fail to
    /// start anyway — and it is considerably less code. Every long-running startup step
    /// flips a flag here from a background thread, so the UI can show a skeleton for
    /// exactly the parts that are not ready yet.
    /// </para>
    /// <para>
    /// <see cref="DatabaseReady"/> exists because non-blocking startup introduces a race
    /// the old synchronous version could not have: the window loads and starts tracking
    /// while migrations are still running, and the first session insert hits tables that
    /// do not exist yet. Anything that touches the database awaits this first. It
    /// completes with <c>false</c> rather than faulting when migration fails, so a
    /// waiter's failure path is an <c>if</c> rather than a try/catch.
    /// </para>
    /// </remarks>
    public sealed partial class StartupState : ObservableObject
    {
        private readonly TaskCompletionSource<bool> _databaseReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        [ObservableProperty]
        private bool _isDatabaseReady;

        [ObservableProperty]
        private bool _isEmbeddingReady;

        /// <summary>
        /// Set when a startup step failed. The app stays usable in whatever reduced form
        /// it can manage — a failed embedding warm-up still leaves the keyword ruleset —
        /// so this is surfaced as a banner, never as a fatal dialog.
        /// </summary>
        [ObservableProperty]
        private string? _failureMessage;

        /// <summary>
        /// Completes with true once the schema is usable, false if it could not be.
        /// Never faults, and safe to await from any number of callers.
        /// </summary>
        public Task<bool> DatabaseReady => _databaseReady.Task;

        public void MarkDatabaseReady()
        {
            IsDatabaseReady = true;
            _databaseReady.TrySetResult(true);
        }

        public void MarkDatabaseFailed(string message)
        {
            FailureMessage = message;
            _databaseReady.TrySetResult(false);
        }
    }
}
