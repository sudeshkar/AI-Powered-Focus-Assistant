using System;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Turns a classification into a distraction signal, tracking how long the current
    /// stretch has run and how often the user has been switching.
    /// </summary>
    /// <remarks>
    /// Stateful by necessity - "how long has this been going on" only means something if
    /// something remembers when it started - so implementations are registered as
    /// singletons and must be safe to call from a timer thread.
    /// </remarks>
    public interface IDistractionDetector
    {
        /// <summary>
        /// Feeds the current foreground activity and its verdict. Called on a short
        /// interval (not only on window switch), so a signal exists while the user sits
        /// still in one distracting window.
        /// </summary>
        /// <returns>
        /// Null when the current activity is not distracting - the common case, and the
        /// state resets so the next distracting stretch starts its clock from zero.
        /// </returns>
        DistractionSignal? Observe(ActivityContext context, ProductivityVerdict verdict, DateTimeOffset now);

        /// <summary>Records a completed app switch, for the switches-per-hour component of risk.</summary>
        void RecordSwitch(DateTimeOffset when);

        /// <summary>
        /// The last application seen that was classified productive - what a "Back to X"
        /// nudge button would foreground. Null before anything productive has been observed.
        /// </summary>
        string? LastProductiveApp { get; }
    }
}
