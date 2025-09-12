using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    public class FlaskFeedbackService : IFeedbackService
    {
        private readonly IHttpClientWrapper _httpClient;

        public FlaskFeedbackService(IHttpClientWrapper httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task SendFeedbackAsync(FeedbackRequest feedback)
        {
            try
            {
                var json = JsonSerializer.Serialize(feedback);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("http://127.0.0.1:5000/feedback", content);
                response.EnsureSuccessStatusCode();
                Console.WriteLine(response.StatusCode + "Successfully submitted feedback to RL");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending feedback: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                throw;
            }
        }
    }
}