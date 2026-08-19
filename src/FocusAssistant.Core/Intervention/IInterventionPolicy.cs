using FocusAssistant.Core.Focus;
using System;

namespace FocusAssistant.Core.Intervention
{
    /// <summary>
    /// Decides whether a distraction signal is worth interrupting someone over.
    /// </summary>
    /// <remarks>
    /// Null is the default and the common case: silence, not a suggestion, is what this
    /// returns for almost every call. A policy that speaks often is a policy people turn
    /// off.
    /// </remarks>
    public interface IInterventionPolicy
    {
        InterventionSuggestion? Decide(DistractionSignal signal, DateTimeOffset now);

        /// <summary>Records what the user did about a suggestion, for cadence and de-escalation.</summary>
        void RecordResponse(InterventionSuggestion suggestion, InterventionResponse response, DateTimeOffset when);
    }
}
