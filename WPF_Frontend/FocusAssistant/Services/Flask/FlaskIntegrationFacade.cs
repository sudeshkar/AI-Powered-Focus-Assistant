using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    /// <summary>
    /// Single entry point to the backend for view models, so they depend on one
    /// service rather than four plus the process manager.
    /// </summary>
    public class FlaskIntegrationFacade : IActivityService, ISuggestionsService, IAnalyticsService, IFeedbackService
    {
        private readonly IActivityService _activityService;
        private readonly ISuggestionsService _suggestionsService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IFeedbackService _feedbackService;
        private readonly IFlaskServerManager _serverManager;

        public FlaskIntegrationFacade(
            IActivityService activityService,
            ISuggestionsService suggestionsService,
            IAnalyticsService analyticsService,
            IFeedbackService feedbackService,
            IFlaskServerManager serverManager)
        {
            _activityService = activityService ?? throw new ArgumentNullException(nameof(activityService));
            _suggestionsService = suggestionsService ?? throw new ArgumentNullException(nameof(suggestionsService));
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));
            _serverManager = serverManager ?? throw new ArgumentNullException(nameof(serverManager));
        }

        public Task<bool> StartServerAsync() => _serverManager.StartServerAsync();

        public Task<bool> IsServerHealthyAsync() => _serverManager.IsServerHealthyAsync();

        public void StopServer() => _serverManager.StopServer();

        public Task<ActivityResponse?> SendActivityAsync(ActivityRequest activityRequest) =>
            _activityService.SendActivityAsync(activityRequest);

        public Task<SuggestionsResponse?> GetSuggestionsAsync() =>
            _suggestionsService.GetSuggestionsAsync();

        public Task<AnalyticsResponse?> GetAnalyticsAsync() =>
            _analyticsService.GetAnalyticsAsync();

        public Task<InsightsResponse?> GetInsightsAsync() =>
            _analyticsService.GetInsightsAsync();

        public Task SendFeedbackAsync(FeedbackRequest feedback) =>
            _feedbackService.SendFeedbackAsync(feedback);
    }
}
