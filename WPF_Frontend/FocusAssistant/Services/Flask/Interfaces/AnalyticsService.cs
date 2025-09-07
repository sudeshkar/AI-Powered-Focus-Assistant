using Amazon.Runtime.Internal;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    public class AnalyticsService : BaseFlaskApiService ,IAnalyticsService
    {
        protected AnalyticsService(FlaskConfiguration config, IHttpClientWrapper httpClient, IFlaskServerManager serverManager) : base(config, httpClient, serverManager)
        {
        }

        public async Task<AnalyticsResponse?> GetAnalyticsAsync()
        {
             var res = await ExecuteGetRequest<AnalyticsResponse>("analytics");
                   return res;
            
        }

        public Task<InsightsResponse> GetInsightsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
