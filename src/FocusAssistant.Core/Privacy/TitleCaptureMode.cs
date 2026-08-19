namespace FocusAssistant.Core.Privacy
{
    /// <summary>
    /// How much of a window's title gets written to disk.
    /// </summary>
    /// <remarks>
    /// Domain vocabulary, not configuration - it lives in Core so
    /// <see cref="IActivityPrivacyFilter"/> and <c>SessionEngine</c> can both refer to it
    /// without Core depending on the App project's options types.
    /// </remarks>
    public enum TitleCaptureMode
    {
        /// <summary>Store the title verbatim.</summary>
        Full,

        /// <summary>Store only the application name; the title is dropped entirely.</summary>
        AppOnly,

        /// <summary>Store the classifier's category instead of the title text.</summary>
        Redacted,
    }
}
