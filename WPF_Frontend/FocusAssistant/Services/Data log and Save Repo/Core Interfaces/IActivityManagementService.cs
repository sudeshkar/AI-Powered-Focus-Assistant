using FocusAssistant.Enums;
using FocusAssistant.Models;
using FocusAssistant.Services.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces
{
    public interface IActivityManagementService 
    {
        Task LogActivityAsync(AppUsage activity);
        Task LogActivitiesAsync(IEnumerable<AppUsage> activities);
        Task LogIdleStateAsync(IdleStateChangedEventArgs idleEvent);
        Task SaveSessionAsync(WorkSession session);
        Task SaveSessionFromActivitiesAsync(IEnumerable<AppUsage> activities);
        Task<IEnumerable<WorkSession>> GetRecentSessionsAsync(int days = 7);
        Task<IEnumerable<WorkSession>> GetSessionsByDateAsync(DateTime date);
        Task ExportDataAsync(ExportType exportType, ExportFormat format, string filePath, int days = 30);



    }
}
