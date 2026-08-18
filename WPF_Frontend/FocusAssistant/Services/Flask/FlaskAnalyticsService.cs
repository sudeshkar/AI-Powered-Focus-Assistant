using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    /// <summary>Reads /analytics and /insights from the backend.</summary>
    public class FlaskAnalyticsService : BaseFlaskApiService, IAnalyticsService
    {
        public FlaskAnalyticsService(
            FlaskConfiguration config,
            IHttpClientWrapper httpClient,
            IFlaskServerManager serverManager)
            : base(config, httpClient, serverManager)
        {
        }

        public Task<AnalyticsResponse?> GetAnalyticsAsync() =>
            ExecuteGetRequest<AnalyticsResponse>("analytics");

        // Previously threw NotImplementedException, which surfaced in the
        // Recommendations view as a permanent "method not implemented" error.
        public Task<InsightsResponse?> GetInsightsAsync() =>
            ExecuteGetRequest<InsightsResponse>("insights");
    }
}
