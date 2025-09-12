using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    public class FlaskAnalyticsService : IAnalyticsService
    {
        public readonly IHttpClientWrapper _httpClient;

        public FlaskAnalyticsService(IHttpClientWrapper httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<AnalyticsResponse> GetAnalyticsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/analytics");
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AnalyticsResponse>(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching analytics: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                return new AnalyticsResponse { Status = ex.Message };
            }
        }

        public async Task<InsightsResponse> GetInsightsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/insights");
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<InsightsResponse>(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching insights: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                return new InsightsResponse { Status = ex.Message };
            }
        }
    }
}