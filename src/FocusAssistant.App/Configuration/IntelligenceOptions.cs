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
        /// Below this cosine similarity nothing in the prototype set resembles the input at
        /// all, and the classifier abstains rather than guessing.
        /// </summary>
        /// <remarks>
        /// Measured, not guessed. Real window titles score 0.14-0.40 against these
        /// prototypes - short titles compared against longer descriptions simply do not
        /// reach the 0.7-0.8 range that sentence-pair similarity demos show. A floor set by
        /// intuition rather than measurement silences the classifier on almost everything.
        /// </remarks>
        public double MinimumSimilarity { get; set; } = 0.12;

        /// <summary>
        /// Minimum gap between the best productive and best distracting prototype. This,
        /// not the absolute score, is what actually separates the cases: a clear match runs
        /// 0.12-0.29 while genuinely ambiguous input sits near 0.03.
        /// </summary>
        /// <remarks>
        /// The gap is measured across polarities rather than against the second-best
        /// prototype overall. Two productive prototypes scoring 0.29 and 0.26 are not
        /// ambiguity - they agree - and treating them as a near-tie would silence the
        /// classifier on exactly the inputs it understands best.
        /// </remarks>
        public double MinimumMargin { get; set; } = 0.06;

        /// <summary>
        /// How long the 2.5GB language model may sit loaded with no requests before
        /// it is unloaded. Resident memory that large is the difference between a
        /// background app people keep and one they uninstall.
        /// </summary>
        public TimeSpan SlmIdleUnloadTimeout { get; set; } = TimeSpan.FromMinutes(10);
    }
}
