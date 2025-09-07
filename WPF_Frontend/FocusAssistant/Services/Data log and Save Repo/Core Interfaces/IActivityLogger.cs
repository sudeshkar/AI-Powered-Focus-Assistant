using FocusAssistant.Models;
using FocusAssistant.Services.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo
{
    public interface IActivityLogger
    {
        Task LogActivityAsync(AppUsage activity);
        Task LogIdleStateAsync(IdleStateChangedEventArgs idleEvent);
        Task LogActivitiesAsync(IEnumerable<AppUsage> activities);
    }
}
