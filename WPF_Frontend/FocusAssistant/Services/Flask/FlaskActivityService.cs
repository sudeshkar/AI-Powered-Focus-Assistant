using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    /// <summary>Posts activity to the backend and returns the agent's decision.</summary>
    public class FlaskActivityService : BaseFlaskApiService, IActivityService
    {
        public FlaskActivityService(
            FlaskConfiguration config,
            IHttpClientWrapper httpClient,
            IFlaskServerManager serverManager)
            : base(config, httpClient, serverManager)
        {
        }

        public Task<ActivityResponse?> SendActivityAsync(ActivityRequest activityRequest) =>
            ExecutePostRequest<ActivityResponse>("activity", activityRequest);
    }
}
