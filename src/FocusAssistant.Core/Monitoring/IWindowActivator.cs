namespace FocusAssistant.Core.Monitoring
{
    /// <summary>
    /// Brings a running application's window to the foreground by process name.
    /// </summary>
    /// <remarks>
    /// What the nudge's "Back to X" button needs and <see cref="IWindowMonitor"/> does not
    /// provide - that interface only reads which window is active, it does not change it.
    /// </remarks>
    public interface IWindowActivator
    {
        /// <summary>
        /// Finds the most recently active top-level window belonging to a process of this
        /// name and activates it. Returns false when no matching window is currently open -
        /// the app may have been closed since the nudge was built.
        /// </summary>
        bool ActivateByProcessName(string processName);
    }
}
