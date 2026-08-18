using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using System;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Session.Interfaces
{
    /// <summary>Starts, ends and reports on tracking sessions.</summary>
    public interface ISessionManager
    {
        event EventHandler<UserSession>? SessionStarted;
        event EventHandler<UserSession>? SessionEnded;

        /// <summary>Raised when the backend returns an intervention worth showing.</summary>
        event EventHandler<ActivityResponse>? AiInterventionReceived;

        bool IsSessionActive { get; }

        Task StartSessionAsync();
        Task EndSessionAsync();

        /// <summary>Aggregates across today's sessions, including the one in progress.</summary>
        SessionStatistics GetTodayStatistics();
    }
}
