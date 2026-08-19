using System;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Everything the intervention policy needs to decide whether to say something.
    /// </summary>
    /// <param name="Context">What the user is doing right now.</param>
    /// <param name="Verdict">How it was classified, and how confidently.</param>
    /// <param name="ContinuousDistractionTime">
    /// How long the current distracting stretch has run. Zero when the current activity is
    /// productive or just started.
    /// </param>
    /// <param name="RecentAppSwitches">
    /// Switches in roughly the last hour. Thrashing between windows is itself a distraction
    /// signal independent of what any one window was.
    /// </param>
    /// <param name="GoalRelevance">0-1 similarity to the session goal, when one is set.</param>
    /// <param name="Risk">
    /// 0-1 composite estimate of how distracted this stretch looks, folding in confidence,
    /// duration, and switching - the single number the policy's thresholds are written
    /// against.
    /// </param>
    public sealed record DistractionSignal(
        ActivityContext Context,
        ProductivityVerdict Verdict,
        TimeSpan ContinuousDistractionTime,
        int RecentAppSwitches,
        double? GoalRelevance,
        double Risk);
}
