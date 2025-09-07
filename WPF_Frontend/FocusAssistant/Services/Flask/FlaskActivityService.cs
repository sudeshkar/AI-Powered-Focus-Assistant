using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    public class FlaskActivityService : IActivityService
    {
        private readonly IHttpClientWrapper _httpClient;

        public FlaskActivityService(IHttpClientWrapper httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ActivityResponse> SendActivityAsync(AppUsage appUsage)
        {
            try
            {
                var json = JsonSerializer.Serialize(appUsage);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/activity", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ActivityResponse>(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending activity: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                return new ActivityResponse { Status = ex.Message };
            }
        }
    }
}