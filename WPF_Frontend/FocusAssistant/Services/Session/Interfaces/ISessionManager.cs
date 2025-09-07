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
        event EventHandler<WorkSession> SessionStarted;
        event EventHandler<WorkSession> SessionEnded;
        event EventHandler<AppUsage> AppUsageAdded;
        event EventHandler<WorkSession> SessionUpdated;

        // Properties
        WorkSession CurrentSession { get; }
        List<WorkSession> TodaySessions { get; }
        bool IsSessionActive { get; }

        // Methods
        void StartSession();
        Task EndSessionAsync();
        void AddAppUsage(AppUsage appUsage);
        SessionStatistics GetTodayStatistics();
    }
}
