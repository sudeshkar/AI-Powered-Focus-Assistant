using FocusAssistant.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FocusAssistant.Services
{
    public class FlaskIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _flaskApiUrl;
        private Process _flaskProcess;
        private bool _isFlaskRunning;
        


        public FlaskIntegrationService(string apiUrl = null)
        {
            _flaskApiUrl = string.IsNullOrWhiteSpace(apiUrl) ? (Environment.GetEnvironmentVariable("FOCUS_API_URL") ?? "http://127.0.0.1:5000") : apiUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public async Task<bool> StartFlaskServerAsync()
        {
            try
            {
                // Check if Flask is already running
                if (await IsFlaskHealthyAsync())
                {
                    _isFlaskRunning = true;
                    Console.WriteLine("✅ Flask server is already running");
                    return true;
                }

                // Start Flask process
                var pythonPath = FindPythonExecutable();
                var configuredPath = Environment.GetEnvironmentVariable("FOCUS_BACKEND_PATH");
                var baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python_Backend", "app.py");
                var repoRelativePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Python_Backend", "app.py"));
                var scriptPath = !string.IsNullOrWhiteSpace(configuredPath) ? configuredPath : (File.Exists(baseDirPath) ? baseDirPath : repoRelativePath);

                if (!File.Exists(scriptPath))
                {
                    Console.WriteLine("❌ Flask app.py not found. Please ensure Python_Backend/app.py exists.");
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\"",
                    WorkingDirectory = Path.GetDirectoryName(scriptPath),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _flaskProcess = Process.Start(startInfo);

                // Wait for Flask to start (max 10 seconds)
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(500);
                    if (await IsFlaskHealthyAsync())
                    {
                        _isFlaskRunning = true;
                        Console.WriteLine("🚀 Flask server started successfully");
                        return true;
                    }
                }

                Console.WriteLine("⏰ Flask server took too long to start");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to start Flask server: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> IsFlaskHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_flaskApiUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private string FindPythonExecutable()
        {
            // Try common Python locations
            var pythonPaths = new[]
            {
                "python",
                "python3",
                @"C:\Python39\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python39\python.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python310\python.exe"
            };

            foreach (var path in pythonPaths)
            {
                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    });

                    if (process != null)
                    {
                        process.WaitForExit(2000);
                        if (process.ExitCode == 0)
                        {
                            return path;
                        }
                    }
                }
                catch
                {
                    // Continue trying next path
                }
            }

            return "python"; // Default fallback
        }

        public async Task<MLPredictionResponse> SendActivityAsync(AppUsage appUsage)
        {
            if (!_isFlaskRunning)
            {
                Console.WriteLine("⚠️ Flask server not running. Starting...");
                await StartFlaskServerAsync();
            }

            try
            {
                var activityData = new
                {
                    app_name = appUsage.AppName,
                    window_title = appUsage.WindowTitle,
                    is_productive = appUsage.IsProductive,
                    duration_minutes = appUsage.Duration.TotalMinutes,
                    timestamp = appUsage.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var json = JsonConvert.SerializeObject(activityData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_flaskApiUrl}/activity", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<MLPredictionResponse>(responseJson);

                    Console.WriteLine($"🤖 ML Response: Risk={result.DistractionRisk:F2}, Message={result.InterventionMessage}");
                    return result;
                }
                else
                {
                    Console.WriteLine($"❌ Flask API error: {response.StatusCode}");
                    return new MLPredictionResponse { Status = "error" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send activity to Flask: {ex.Message}");
                return new MLPredictionResponse { Status = "error", Error = ex.Message };
            }
        }

        public async Task<ProductivitySuggestions> GetSuggestionsAsync()
        {
            if (!_isFlaskRunning) return null;

            try
            {
                var response = await _httpClient.GetAsync($"{_flaskApiUrl}/suggestions");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ProductivitySuggestions>(json);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to get suggestions: {ex.Message}");
            }

            return null;
        }

        public async Task<AnalyticsResponse> GetAnalyticsAsync()
        {
            if (!_isFlaskRunning) return null;

            try
            {
                var response = await _httpClient.GetAsync($"{_flaskApiUrl}/analytics");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<AnalyticsResponse>(json);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to get analytics: {ex.Message}");
            }

            return null;
        }

        public async Task SendFeedbackAsync(bool helpful, string action, string feedback = "", string interventionId = null)
        {
            if (!_isFlaskRunning) return;

            try
            {
                var feedbackData = new
                {
                    helpful = helpful,
                    action = action,
                    feedback = feedback,
                    intervention_id = interventionId   // matches Flask JSON field
                };

                var json = JsonConvert.SerializeObject(feedbackData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync($"{_flaskApiUrl}/feedback", content);
                Console.WriteLine($"📝 Feedback sent: {action}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send feedback: {ex.Message}");
            }
        }

        public void StopFlaskServer()
        {
            try
            {
                _flaskProcess?.Kill();
                _flaskProcess?.Dispose();
                _isFlaskRunning = false;
                Console.WriteLine("🛑 Flask server stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error stopping Flask server: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopFlaskServer();
            _httpClient?.Dispose();
        }
    }

    // Response Models
    public class MLPredictionResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("distraction_risk")]
        public double DistractionRisk { get; set; }

        [JsonProperty("intervention_message")]
        public string InterventionMessage { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("intervention_id")]    
        public string InterventionId { get; set; }
    }

    public class ProductivitySuggestions
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("suggestions")]
        public string[] Suggestions { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }

    public class AnalyticsResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("total_activities")]
        public int TotalActivities { get; set; }

        [JsonProperty("productivity_rate")]
        public double ProductivityRate { get; set; }

        [JsonProperty("top_apps")]
        public Dictionary<string, int> TopApps { get; set; }

        [JsonProperty("recent_interventions")]
        public int RecentInterventions { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }
}
