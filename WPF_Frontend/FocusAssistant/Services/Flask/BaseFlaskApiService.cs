using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    /// <summary>
    /// Shared HTTP plumbing for the backend endpoints: ensures the server is up,
    /// builds URLs from configuration, and serialises consistently.
    /// </summary>
    public abstract class BaseFlaskApiService
    {
        /// <summary>
        /// The DTOs are annotated with System.Text.Json's [JsonPropertyName] to map
        /// the backend's snake_case fields. GET responses used to be deserialised
        /// with Newtonsoft, which ignores those attributes, so every property came
        /// back as its default value with no error.
        /// </summary>
        protected static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        protected readonly FlaskConfiguration _config;
        protected readonly IHttpClientWrapper _httpClient;
        protected readonly IFlaskServerManager _serverManager;

        protected BaseFlaskApiService(
            FlaskConfiguration config,
            IHttpClientWrapper httpClient,
            IFlaskServerManager serverManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _serverManager = serverManager ?? throw new ArgumentNullException(nameof(serverManager));
        }

        protected async Task<T?> ExecuteGetRequest<T>(string endpoint) where T : class
        {
            if (!await EnsureServerRunning())
                return null;

            try
            {
                using var response = await _httpClient.GetAsync(Url(endpoint));
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"GET {endpoint} returned {(int)response.StatusCode}.");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GET {endpoint} failed: {ex.Message}");
                return null;
            }
        }

        protected async Task<T?> ExecutePostRequest<T>(string endpoint, object data) where T : class
        {
            if (!await EnsureServerRunning())
                return null;

            try
            {
                using var content = Serialize(data);
                using var response = await _httpClient.PostAsync(Url(endpoint), content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"POST {endpoint} returned {(int)response.StatusCode}.");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"POST {endpoint} failed: {ex.Message}");
                return null;
            }
        }

        protected async Task<bool> ExecutePostRequest(string endpoint, object data)
        {
            if (!await EnsureServerRunning())
                return false;

            try
            {
                using var content = Serialize(data);
                using var response = await _httpClient.PostAsync(Url(endpoint), content);
                if (!response.IsSuccessStatusCode)
                    Console.WriteLine($"POST {endpoint} returned {(int)response.StatusCode}.");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"POST {endpoint} failed: {ex.Message}");
                return false;
            }
        }

        private string Url(string endpoint) => $"{_config.ApiUrl}/{endpoint.TrimStart('/')}";

        private static StringContent Serialize(object data) =>
            new(JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8, "application/json");

        private async Task<bool> EnsureServerRunning()
        {
            if (await _serverManager.IsServerHealthyAsync())
                return true;

            Console.WriteLine("Backend not responding; attempting to start it.");
            return await _serverManager.StartServerAsync();
        }
    }
}
