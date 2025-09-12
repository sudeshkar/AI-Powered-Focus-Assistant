using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Session.Interfaces
{
    public interface ISessionManager
    {
        // Events
        event EventHandler<UserSession> SessionStarted;
        event EventHandler<UserSession> SessionEnded;
        public event EventHandler<ActivityResponse>? AiInterventionReceived;

        Task StartSessionAsync();
        Task EndSessionAsync();
        SessionStatistics GetTodayStatistics();
        void AddAppUsage(string appname,AppUsage currentAppUsage);
    }
}
