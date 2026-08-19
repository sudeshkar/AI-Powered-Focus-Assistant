namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// A classification result, with enough context to know how much to trust it.
    /// </summary>
    /// <param name="IsProductive">Whether the activity counts towards productive time.</param>
    /// <param name="Confidence">0-1. Low values mean the verdict should not drive an interruption.</param>
    /// <param name="Source">Which layer decided; see <see cref="ClassificationSource"/>.</param>
    /// <param name="Rationale">
    /// Human-readable reason, shown in the UI so the user can see why the app thinks what
    /// it thinks - and correct it. "Distracting - closest match: watching an entertainment
    /// video" is arguable; a bare red dot is not.
    /// </param>
    public readonly record struct ProductivityVerdict(
        bool IsProductive,
        double Confidence,
        ClassificationSource Source,
        string? Rationale)
    {
        /// <summary>
        /// The fallback when no layer recognised the activity: assume productive, so nobody
        /// is punished for tools the app has not been taught about - but record that it was
        /// a guess, which the old bare <c>true</c> could not.
        /// </summary>
        public static ProductivityVerdict Guess() =>
            new(true, 0.2, ClassificationSource.Default, "no rule or model match");

        /// <summary>True when this verdict is solid enough to interrupt someone over.</summary>
        public bool IsActionable => Source != ClassificationSource.Default && Confidence >= 0.5;
    }
}
