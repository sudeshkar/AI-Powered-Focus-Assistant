using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    /// <summary>
    /// Owns the lifetime of the Python backend process.
    /// </summary>
    public class FlaskServerManager : IFlaskServerManager, IDisposable
    {
        private readonly IPythonExecutableFinder _pythonFinder;
        private readonly FlaskConfiguration _config;

        // Health checks must not reuse the shared client: that one is configured for
        // API calls, and a probe against a dead port should fail fast, not after 30s.
        private readonly HttpClient _healthClient = new() { Timeout = TimeSpan.FromSeconds(2) };

        // Serialises concurrent callers so two request paths cannot both spawn a server.
        private readonly SemaphoreSlim _startLock = new(1, 1);

        // Backstop: Windows terminates the backend with this process even if the
        // normal shutdown path never runs.
        private readonly ChildProcessJob _job = new();

        private Process? _flaskProcess;
        private bool _disposed;

        public FlaskServerManager(IPythonExecutableFinder pythonFinder, FlaskConfiguration config)
        {
            _pythonFinder = pythonFinder ?? throw new ArgumentNullException(nameof(pythonFinder));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Probes /health. Previously this threw NotImplementedException, which
        /// every BaseFlaskApiService call hit before its try block.
        /// </summary>
        public async Task<bool> IsServerHealthyAsync()
        {
            try
            {
                using var response = await _healthClient.GetAsync($"{_config.ApiUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                // Nothing listening, or it did not answer in time.
                return false;
            }
        }

        public async Task<bool> StartServerAsync()
        {
            if (_disposed)
                return false;

            await _startLock.WaitAsync();
            try
            {
                // Another caller may have started it, or the user may be running it manually.
                if (await IsServerHealthyAsync())
                    return true;

                if (_flaskProcess is { HasExited: false })
                {
                    Console.WriteLine("Backend process is running but not answering /health yet.");
                    return await WaitForHealthyAsync();
                }

                if (!_config.IsBackendAvailable)
                {
                    Console.WriteLine("Python backend not found; skipping start.");
                    return false;
                }

                var pythonPath = _pythonFinder.FindPythonExecutable();
                if (string.IsNullOrEmpty(pythonPath))
                {
                    Console.WriteLine("No Python interpreter found; skipping start.");
                    return false;
                }

                return StartProcess(pythonPath) && await WaitForHealthyAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Flask server: {ex.Message}");
                return false;
            }
            finally
            {
                _startLock.Release();
            }
        }

        private bool StartProcess(string pythonPath)
        {
            // Run app.py directly from the backend directory. The previous
            // "-m flask run" invocation had no FLASK_APP and no working directory,
            // so it could never have located the application.
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                WorkingDirectory = _config.WorkingDirectory!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(_config.ScriptPath!);

            // Unbuffered, so the child's output reaches us as it happens.
            startInfo.Environment["PYTHONUNBUFFERED"] = "1";

            _flaskProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _flaskProcess.OutputDataReceived += (_, e) => Log("backend", e.Data);
            _flaskProcess.ErrorDataReceived += (_, e) => Log("backend", e.Data);

            if (!_flaskProcess.Start())
            {
                Console.WriteLine("Failed to start the backend process.");
                return false;
            }

            // Both streams are redirected, so both must be drained or the child
            // blocks once a pipe buffer fills.
            _flaskProcess.BeginOutputReadLine();
            _flaskProcess.BeginErrorReadLine();

            // Assign before anything can go wrong, so the process is covered by the
            // job for its whole life. On Windows the venv launcher spawns the real
            // interpreter as a child, and job membership is inherited.
            _job.TryAssign(_flaskProcess);

            Console.WriteLine($"Backend starting (pid {_flaskProcess.Id}).");
            return true;
        }

        private async Task<bool> WaitForHealthyAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(_config.StartupTimeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                if (_flaskProcess is { HasExited: true })
                {
                    Console.WriteLine($"Backend exited during startup with code {_flaskProcess.ExitCode}.");
                    return false;
                }

                if (await IsServerHealthyAsync())
                {
                    Console.WriteLine("Backend is healthy.");
                    return true;
                }

                await Task.Delay(250);
            }

            Console.WriteLine($"Backend did not become healthy within {_config.StartupTimeoutSeconds}s.");
            return false;
        }

        public void StopServer()
        {
            var process = _flaskProcess;
            _flaskProcess = null;

            if (process is null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    // Kill the tree: the Windows venv launcher spawns the real
                    // interpreter as a child, and killing only the parent orphans it
                    // and leaves the port bound.
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                    Console.WriteLine("Backend stopped.");
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping backend: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        private static void Log(string source, string? line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                Console.WriteLine($"[{source}] {line}");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopServer();

            // Closing the job kills anything StopServer missed.
            _job.Dispose();
            _healthClient.Dispose();
            _startLock.Dispose();
        }
    }
}
