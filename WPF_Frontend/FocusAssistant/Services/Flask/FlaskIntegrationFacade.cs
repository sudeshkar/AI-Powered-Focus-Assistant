using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    public class FlaskIntegrationFacade : IActivityService, ISuggestionsService, IAnalyticsService, IFeedbackService, IDisposable
    {
        private readonly ISuggestionsService _suggestionsService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IFeedbackService _feedbackService;
        private readonly IActivityService _activityService;
        private readonly IFlaskServerManager _serverManager;

        public FlaskIntegrationFacade(
            IActivityService activityService,
            ISuggestionsService suggestionsService,
            IAnalyticsService analyticsService,
            IFeedbackService feedbackService,
            IFlaskServerManager serverManager)
        {
            _activityService = activityService;
            _suggestionsService = suggestionsService;
            _analyticsService = analyticsService;
            _feedbackService = feedbackService;
            _serverManager = serverManager;
        }

        public void Dispose()
        {
            if (_serverManager is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public async Task<bool> StartServerAsync() =>
            await _serverManager.StartServerAsync();

        public void StopServer() =>
            _serverManager.StopServer();

        public async Task<ActivityResponse> SendActivityAsync(AppUsage appUsage) =>
            await _activityService.SendActivityAsync(appUsage);

        public async Task<SuggestionsResponse> GetSuggestionsAsync() =>
            await _suggestionsService.GetSuggestionsAsync();

        public async Task<AnalyticsResponse> GetAnalyticsAsync() =>
            await _analyticsService.GetAnalyticsAsync();

        public async Task<InsightsResponse> GetInsightsAsync() =>
            await _analyticsService.GetInsightsAsync();

        public async Task SendFeedbackAsync(FeedbackRequest feedback) =>
            await _feedbackService.SendFeedbackAsync(feedback);
    }
}