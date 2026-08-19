namespace FocusAssistant.Core.Focus
{
    /// <summary>What the keyword ruleset was able to say about an application.</summary>
    public enum RuleMatchKind
    {
        /// <summary>Listed as a productive application.</summary>
        ExplicitProductive,

        /// <summary>Listed as distracting, and the title gave no reason to reconsider.</summary>
        ExplicitDistracting,

        /// <summary>Listed as distracting, but the window title looks work-related.</summary>
        TitleRescued,

        /// <summary>The ruleset has never heard of this application.</summary>
        NoMatch,
    }

    /// <summary>
    /// The ruleset's answer. Kept separate from a bool so <see cref="RuleMatchKind.NoMatch"/>
    /// can fall through to a layer that might actually know.
    /// </summary>
    public readonly record struct RuleMatch(RuleMatchKind Kind, string Category);
}
