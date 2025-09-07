using FocusAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces
{
    public interface IActivityRepository
    {
        Task SaveActivityAsync(AppUsage activity);
        Task SaveActivitiesAsync(IEnumerable<AppUsage> activities);
        Task<IEnumerable<AppUsage>> GetActivitiesAsync(DateTime from, DateTime to);
        Task<IEnumerable<AppUsage>> GetActivitiesByDateAsync(DateTime date);
        void DeleteOldActivities(DateTime cutoffDate);
    }
}
