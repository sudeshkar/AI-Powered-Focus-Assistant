namespace FocusAssistant.Core.Privacy
{
    /// <summary>
    /// What actually gets written to disk for one stretch of activity.
    /// </summary>
    /// <param name="AppName">
    /// The name to store. Real for normal activity; a fixed placeholder for an excluded
    /// process, so a password manager's own name never appears in the log either.
    /// </param>
    /// <param name="WindowTitle">
    /// The title to store, already reduced according to <see cref="TitleCaptureMode"/> -
    /// verbatim, empty, or replaced with the activity's category.
    /// </param>
    /// <param name="IsExcluded">
    /// True for a process on the excluded list. Kept separate from the redaction fields so
    /// callers (the intervention pipeline, in particular) can skip excluded activity
    /// entirely rather than merely storing it quietly - nobody should be nudged about their
    /// password manager.
    /// </param>
    public readonly record struct PrivacyDecision(string AppName, string? WindowTitle, bool IsExcluded);

    /// <summary>
    /// Decides what a raw (app name, window title) pair becomes before it is ever
    /// persisted or classified.
    /// </summary>
    /// <remarks>
    /// Applied at the single point <c>SessionEngine</c> builds an <c>AppUsage</c> row -
    /// there is no second place in the app that writes activity to disk, so there is no
    /// second place this needs to be enforced.
    /// </remarks>
    public interface IActivityPrivacyFilter
    {
        PrivacyDecision Apply(string appName, string? windowTitle, string category);
    }
}
