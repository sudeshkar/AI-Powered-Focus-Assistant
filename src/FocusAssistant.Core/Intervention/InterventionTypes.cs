using System;

namespace FocusAssistant.Core.Intervention
{
    /// <summary>
    /// How forcefully a suggestion should be surfaced. Populated by Phase 4's
    /// escalation ladder (ambient tint -> toast -> full-screen overlay); for now
    /// callers that only need "was there a suggestion" can ignore it.
    /// </summary>
    public enum InterventionTier
    {
        None,
        Ambient,
        Toast,
        Overlay,
    }

    /// <summary>How the user responded to a shown suggestion.</summary>
    public enum InterventionResponse
    {
        ActedImmediately,
        ActedLater,
        DismissedPolitely,
        Ignored,
    }

    /// <summary>
    /// A candidate nudge produced locally by <see cref="Focus.DistractionDetector"/>
    /// and <see cref="Focus.InterventionPolicy"/> (Phase 4). Replaces the old
    /// backend-sourced ActivityResponse — everything here is computed on-device.
    /// </summary>
    public sealed record InterventionSuggestion
    {
        public required string Message { get; init; }
        public InterventionTier Tier { get; init; } = InterventionTier.Toast;

        /// <summary>0-1 estimate of how distracted the current stretch looks.</summary>
        public double DistractionRisk { get; init; }

        public string? ActionTaken { get; init; }

        /// <summary>The application this nudge was triggered by - what "This is work" applies to.</summary>
        public required string AppName { get; init; }

        /// <summary>Why the policy decided to speak, shown nowhere but logged for Insights.</summary>
        public string? Rationale { get; init; }

        /// <summary>
        /// The last productive application seen before this distracting stretch began -
        /// what "Back to &lt;app&gt;" foregrounds. Null when nothing productive preceded it.
        /// </summary>
        public string? ReturnApp { get; init; }
    }
}
