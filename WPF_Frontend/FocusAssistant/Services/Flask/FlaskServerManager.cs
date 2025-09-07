using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    public class FlaskServerManager : IFlaskServerManager, IDisposable
    {
        private readonly IPythonExecutableFinder _pythonFinder;
        private Process _flaskProcess;

        public FlaskServerManager(IPythonExecutableFinder pythonFinder)
        {
            _pythonFinder = pythonFinder;
        }

        public async Task<bool> StartServerAsync()
        {
            try
            {
                var pythonPath = _pythonFinder.FindPythonExecutable();
                if (string.IsNullOrEmpty(pythonPath))
                {
                    Console.WriteLine("Python executable not found.");
                    return false;
                }

                _flaskProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "-m flask run --host=127.0.0.1 --port=5000",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                _flaskProcess.Start();
                Console.WriteLine($"Flask server started at {DateTime.Now:HH:mm:ss.fff}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Flask server: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                return false;
            }
        }

        public void StopServer()
        {
            if (_flaskProcess != null && !_flaskProcess.HasExited)
            {
                _flaskProcess.Kill();
                _flaskProcess.Dispose();
                Console.WriteLine($"Flask server stopped at {DateTime.Now:HH:mm:ss.fff}");
            }
        }

        public void Dispose()
        {
            StopServer();
        }

        public Task<bool> IsServerHealthyAsync()
        {
            throw new NotImplementedException();
        }
    }
}