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

        public async Task<ActivityResponse> SendActivityAsync(ActivityRequest activityRequest)
        {
            try
            {
                var json = JsonSerializer.Serialize(activityRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("http://127.0.0.1:5000/activity", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                if (responseJson != null)
                {
                    return JsonSerializer.Deserialize<ActivityResponse>(responseJson);
                }
                return new ActivityResponse
                {
                    Status ="Error"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending activity: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                return new ActivityResponse { Status = ex.Message };
            }
        }
    }
}