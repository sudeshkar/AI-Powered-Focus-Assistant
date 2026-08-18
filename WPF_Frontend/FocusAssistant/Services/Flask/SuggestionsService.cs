using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    /// <summary>Reads /suggestions from the backend.</summary>
    public class SuggestionsService : BaseFlaskApiService, ISuggestionsService
    {
        public SuggestionsService(
            FlaskConfiguration config,
            IHttpClientWrapper httpClient,
            IFlaskServerManager serverManager)
            : base(config, httpClient, serverManager)
        {
        }

        public Task<SuggestionsResponse?> GetSuggestionsAsync() =>
            ExecuteGetRequest<SuggestionsResponse>("suggestions");
    }
}
