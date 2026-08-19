using System;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Everything the classifier layers are allowed to see about one stretch of activity.
    /// </summary>
    /// <remarks>
    /// A record rather than passing (appName, windowTitle) pairs around: the layers below
    /// the ruleset need the session goal and the category too, and threading four
    /// parameters through five call sites is how signatures drift apart.
    /// </remarks>
    public sealed record ActivityContext(
        string AppName,
        string? WindowTitle,
        string Category,
        DateTimeOffset StartedAt,
        string? SessionGoal);
}
