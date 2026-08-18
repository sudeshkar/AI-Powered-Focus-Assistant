using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    /// <summary>Posts the user's response to an intervention back to the agent.</summary>
    public class FlaskFeedbackService : BaseFlaskApiService, IFeedbackService
    {
        public FlaskFeedbackService(
            FlaskConfiguration config,
            IHttpClientWrapper httpClient,
            IFlaskServerManager serverManager)
            : base(config, httpClient, serverManager)
        {
        }

        public async Task SendFeedbackAsync(FeedbackRequest feedback)
        {
            // Losing a feedback datapoint slows learning slightly; it is never worth
            // surfacing to the user, so this reports rather than throws.
            if (await ExecutePostRequest("feedback", feedback))
                Console.WriteLine($"Feedback sent for intervention {feedback.InterventionId}.");
        }
    }
}
