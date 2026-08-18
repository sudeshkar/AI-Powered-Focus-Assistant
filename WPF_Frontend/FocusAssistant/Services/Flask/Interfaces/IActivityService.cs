using FocusAssistant.Models.Response_Models;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    public interface IActivityService
    {
        /// <summary>
        /// Reports the active application and returns the agent's decision, or
        /// null when the backend is unreachable.
        /// </summary>
        Task<ActivityResponse?> SendActivityAsync(ActivityRequest activityRequest);
    }
}
