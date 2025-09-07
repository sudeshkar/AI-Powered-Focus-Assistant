using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    public interface IFlaskDataService
    {
        Task<AnalyticsData> GetAnalyticsDataAsync();
         
    }

    public class FlaskDataService : IFlaskDataService
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ISuggestionsService _suggestionsService;
        
        

        public FlaskDataService(IAnalyticsService analyticsService, ISuggestionsService suggestionsService)
        {
            _analyticsService = analyticsService;
            _suggestionsService = suggestionsService;
             
        }

         




        public async Task<AnalyticsData> GetAnalyticsDataAsync()
        {
            var analytics = await _analyticsService.GetAnalyticsAsync();
            var insights = await _analyticsService.GetInsightsAsync();

            return new AnalyticsData
            {
                Analytics = analytics,
                Insights = insights
            };
        }

         

    }

    public class DashboardData
    {
        public AnalyticsResponse Analytics { get; set; }
        public List<string> Suggestions { get; set; }
    }


    public class AnalyticsData
    {
        public AnalyticsResponse Analytics { get; set; }
        public InsightsResponse Insights { get; set; }
    }
}
