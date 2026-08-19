namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Which layer produced a verdict.
    /// </summary>
    /// <remarks>
    /// This exists so that "I know" and "I guessed" stop being indistinguishable. The old
    /// bool-returning strategy collapsed an explicit keyword match and a shrug into the
    /// same <c>true</c>, which is why nothing downstream could ever say anything
    /// interesting - and why the intervention policy refuses to nudge on
    /// <see cref="Default"/>.
    /// </remarks>
    public enum ClassificationSource
    {
        /// <summary>The user corrected this app or title themselves.</summary>
        UserOverride,

        /// <summary>An explicit keyword rule matched.</summary>
        Rule,

        /// <summary>The embedding model recognised the activity.</summary>
        Embedding,

        /// <summary>Decided by similarity to the stated session goal.</summary>
        GoalRelevance,

        /// <summary>Nothing matched. A guess, and flagged as one.</summary>
        Default,
    }
}
