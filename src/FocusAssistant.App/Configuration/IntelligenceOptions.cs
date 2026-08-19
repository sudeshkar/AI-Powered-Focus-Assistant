using System;

namespace FocusAssistant.Configuration
{
    /// <summary>Settings for the on-device models.</summary>
    public sealed class IntelligenceOptions
    {
        public const string SectionName = "Intelligence";

        /// <summary>
        /// Turns the embedding classifier off entirely, falling back to the keyword
        /// ruleset. Kept as an escape hatch: if the model file is missing or ORT
        /// fails to load on some machine, the app must still track.
        /// </summary>
        public bool EnableSemanticClassifier { get; set; } = true;

        /// <summary>
        /// Below this cosine similarity the classifier abstains rather than guessing.
        /// </summary>
        public double MinimumSimilarity { get; set; } = 0.35;

        /// <summary>
        /// Minimum gap between the best and second-best label. A near-tie means the
        /// text is ambiguous, and a confident-sounding wrong answer is worse than
        /// silence.
        /// </summary>
        public double MinimumMargin { get; set; } = 0.05;

        /// <summary>
        /// How long the 2.5GB language model may sit loaded with no requests before
        /// it is unloaded. Resident memory that large is the difference between a
        /// background app people keep and one they uninstall.
        /// </summary>
        public TimeSpan SlmIdleUnloadTimeout { get; set; } = TimeSpan.FromMinutes(10);
    }
}
