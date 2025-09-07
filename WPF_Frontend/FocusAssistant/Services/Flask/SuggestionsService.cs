using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    public class SuggestionsService : BaseFlaskApiService, ISuggestionsService
    {
        public SuggestionsService(FlaskConfiguration config, IHttpClientWrapper httpClient, IFlaskServerManager serverManager) : base(config, httpClient, serverManager)
        {
        }

        public async Task<SuggestionsResponse?> GetSuggestionsAsync()
        {
            var response = await ExecuteGetRequest<SuggestionsResponse>("suggestions");

            if (response != null && response.Status == "success")
            {
                return response;
            }
            return null;
        }
    }
}
