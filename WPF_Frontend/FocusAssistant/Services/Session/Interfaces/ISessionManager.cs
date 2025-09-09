using FocusAssistant.Models;
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

        Task StartSessionAsync();
        Task EndSessionAsync();
        SessionStatistics GetTodayStatistics();
        void AddAppUsage(string appname,AppUsage currentAppUsage);
    }
}
