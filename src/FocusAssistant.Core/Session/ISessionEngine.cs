using FocusAssistant.Core.Models;
using System;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Session
{
    /// <summary>Starts, ends and reports on tracking sessions.</summary>
    public interface ISessionEngine
    {
        event EventHandler<UserSession>? SessionStarted;
        event EventHandler<UserSession>? SessionEnded;

        /// <summary>
        /// Raised whenever a completed app-usage stretch is recorded, so the
        /// intervention pipeline (Phase 4: DistractionDetector -> InterventionPolicy
        /// -> InterventionDispatcher) can react without SessionEngine needing to
        /// know anything about how nudges are decided or shown.
        /// </summary>
        event EventHandler<AppUsage>? ActivityRecorded;

        bool IsSessionActive { get; }

        /// <summary>Optional statement of intent for the session (e.g. "write the methods section").</summary>
        string? CurrentGoal { get; }

        Task StartSessionAsync(string? goal = null);
        Task EndSessionAsync();

        /// <summary>Aggregates across today's sessions, including the one in progress.</summary>
        SessionStatistics GetTodayStatistics();
    }
}
