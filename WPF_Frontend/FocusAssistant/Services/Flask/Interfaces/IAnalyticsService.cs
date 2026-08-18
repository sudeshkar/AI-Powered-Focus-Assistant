using FocusAssistant.Models.Response_Models;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    public interface IAnalyticsService
    {
        /// <summary>Today's totals, or null when the backend is unreachable.</summary>
        Task<AnalyticsResponse?> GetAnalyticsAsync();

        /// <summary>Learning metrics, or null when the backend is unreachable.</summary>
        Task<InsightsResponse?> GetInsightsAsync();
    }
}
