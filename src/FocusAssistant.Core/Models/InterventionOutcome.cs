using System;
using System.ComponentModel.DataAnnotations;
using FocusAssistant.Core.Intervention;

namespace FocusAssistant.Core.Models
{
    /// <summary>
    /// A nudge that was shown, and what happened afterwards.
    /// </summary>
    /// <remarks>
    /// The on-device replacement for the RLInteraction rows that used to audit calls to the
    /// deleted Python backend. There is no model being trained on this table - the learning
    /// loop is the user's own "This is work" corrections, applied deterministically - but
    /// without a record of what was shown and how it landed, nothing on the Insights screen
    /// could ever say how well the nudges are working, and nobody could tell whether the
    /// cadence limits are actually holding.
    /// </remarks>
    public class InterventionOutcome
    {
        [Key]
        public int iID { get; set; }

        /// <summary>The work session this happened during.</summary>
        public string wID { get; set; } = string.Empty;

        public DateTime ShownAt { get; set; }

        public string AppName { get; set; } = string.Empty;

        /// <summary>Why the policy decided to speak, e.g. the classifier's rationale.</summary>
        public string TriggerRationale { get; set; } = string.Empty;

        public InterventionTier Tier { get; set; }

        public double DistractionRisk { get; set; }

        public InterventionResponse Response { get; set; }

        public TimeSpan TimeToRespond { get; set; }

        /// <summary>
        /// Whether a productive app became foreground within two minutes of the nudge.
        /// Measured after the fact, not assumed from which button was clicked - a click on
        /// "Back to VS Code" proves intent, not that it happened.
        /// </summary>
        public bool ReturnedToWork { get; set; }
    }
}
