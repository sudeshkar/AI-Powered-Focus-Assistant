using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json;

namespace FocusAssistant.Services.Flask
{
    public class BaseFlaskApiService
    {
        protected readonly FlaskConfiguration _config;
        protected readonly IHttpClientWrapper _httpClient;
        protected readonly IFlaskServerManager _serverManager;

        protected BaseFlaskApiService(
            FlaskConfiguration config,
            IHttpClientWrapper httpClient,
            IFlaskServerManager serverManager)
        {
            _config = config;
            _httpClient = httpClient;
            _serverManager = serverManager;
        }
        protected async Task<T?> ExecuteGetRequest<T>(string endpoint) where T : class
        {
            if (!await EnsureServerRunning()) return null;

            try
            {
                var response = await _httpClient.GetAsync($"{_config.ApiUrl}/{endpoint}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to execute GET request to {endpoint}: {ex.Message}");
            }

            return null;
        }

        protected async Task<T?> ExecutePostRequest<T>(string endpoint, object data) where T : class
        {
            if (!await EnsureServerRunning()) return null;

            try
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_config.ApiUrl}/{endpoint}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return System.Text.Json.JsonSerializer.Deserialize<T>(responseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to execute POST request to {endpoint}: {ex.Message}");
            }

            return null;
        }

        protected async Task ExecutePostRequest(string endpoint, object data)
        {
            if (!await EnsureServerRunning()) return;

            try
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync($"{_config.ApiUrl}/{endpoint}", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to execute POST request to {endpoint}: {ex.Message}");
            }
        }

        private async Task<bool> EnsureServerRunning()
        {
            if (await _serverManager.IsServerHealthyAsync()) return true;

            Console.WriteLine("⚠️ Flask server not running. Starting...");
            return await _serverManager.StartServerAsync();
        }
    }
}
