using System;
using System.IO;

namespace FocusAssistant.Services.Config
{
    /// <summary>
    /// Where the Python backend lives and how to reach it. Host and port are read
    /// from the repository-root .env so the client and backend cannot disagree.
    /// </summary>
    public class FlaskConfiguration
    {
        public string Host { get; }
        public int Port { get; }

        /// <summary>Base URL with no trailing slash, e.g. "http://127.0.0.1:5000".</summary>
        public string ApiUrl => $"http://{Host}:{Port}";

        /// <summary>Path to app.py, or null when the backend could not be located.</summary>
        public string? ScriptPath { get; }

        /// <summary>Directory to run the backend from, or null when unavailable.</summary>
        public string? WorkingDirectory { get; }

        public int StartupTimeoutSeconds { get; } = 30;
        public int HttpTimeoutSeconds { get; } = 30;

        public bool IsBackendAvailable => ScriptPath is not null;

        public FlaskConfiguration()
        {
            WorkingDirectory = RepositoryLayout.BackendDirectory;
            ScriptPath = RepositoryLayout.BackendScriptPath;

            var env = LoadDotEnv();
            Host = Get(env, "FLASK_HOST", "127.0.0.1");
            Port = int.TryParse(Get(env, "FLASK_PORT", "5000"), out var port) ? port : 5000;
        }

        private static string Get(System.Collections.Generic.IDictionary<string, string> env, string key, string fallback)
        {
            if (env.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();

            return Environment.GetEnvironmentVariable(key) is { Length: > 0 } fromProcess
                ? fromProcess
                : fallback;
        }

        /// <summary>
        /// Minimal .env reader — enough for the handful of KEY=VALUE lines this app
        /// uses, and not worth taking a dependency for.
        /// </summary>
        private static System.Collections.Generic.Dictionary<string, string> LoadDotEnv()
        {
            var values = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var backendDir = RepositoryLayout.BackendDirectory;
            if (backendDir is null)
                return values;

            var envPath = Path.Combine(Path.GetDirectoryName(backendDir)!, ".env");
            if (!File.Exists(envPath))
                return values;

            try
            {
                foreach (var raw in File.ReadAllLines(envPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                        continue;

                    var separator = line.IndexOf('=');
                    if (separator <= 0)
                        continue;

                    values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ignoring unreadable {envPath}: {ex.Message}");
            }

            return values;
        }
    }
}
