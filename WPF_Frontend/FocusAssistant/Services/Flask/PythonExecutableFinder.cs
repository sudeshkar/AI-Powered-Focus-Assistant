using FocusAssistant.Services.Config;
using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace FocusAssistant.Services.Flask
{
    /// <summary>
    /// Finds an interpreter to run the backend with, preferring a virtual
    /// environment inside Python_Backend over whatever is on PATH.
    /// </summary>
    public class PythonExecutableFinder : IPythonExecutableFinder
    {
        private string? _cached;

        /// <summary>Path to a Python executable, or null when none was found.</summary>
        public string? FindPythonExecutable()
        {
            if (_cached is not null)
                return _cached;

            foreach (var candidate in GetCandidatePaths())
            {
                if (File.Exists(candidate))
                {
                    Console.WriteLine($"Using Python at {candidate}");
                    return _cached = candidate;
                }
            }

            var onPath = FindPythonOnPath();
            if (onPath is not null)
            {
                Console.WriteLine($"Using Python from PATH at {onPath}");
                return _cached = onPath;
            }

            Console.WriteLine(
                "No Python interpreter found. Create a virtual environment in " +
                "Python_Backend\\.venv (see README) or install Python system-wide.");
            return null;
        }

        private static IEnumerable<string> GetCandidatePaths()
        {
            var backendDir = RepositoryLayout.BackendDirectory;
            if (backendDir is null)
                yield break;

            // .venv is the name the README tells people to use; venv is the older
            // convention and still worth honouring.
            foreach (var venvName in new[] { ".venv", "venv" })
            {
                yield return Path.Combine(backendDir, venvName, "Scripts", "python.exe");
                yield return Path.Combine(backendDir, venvName, "bin", "python");
            }
        }

        private static string? FindPythonOnPath()
        {
            foreach (var exe in new[] { "python.exe", "python3.exe", "python" })
            {
                var resolved = SearchPathVariable(exe);
                if (resolved is not null)
                    return resolved;
            }

            return null;
        }

        private static string? SearchPathVariable(string fileName)
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVariable))
                return null;

            foreach (var directory in pathVariable.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                string candidate;
                try
                {
                    candidate = Path.Combine(directory.Trim(), fileName);
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry; skip it.
                    continue;
                }

                // Windows ships zero-byte python.exe app-execution aliases in
                // WindowsApps that open the Store instead of running anything.
                if (File.Exists(candidate) && new FileInfo(candidate).Length > 0)
                    return candidate;
            }

            return null;
        }

        /// <summary>Checks that the interpreter runs and has Flask importable.</summary>
        public async System.Threading.Tasks.Task<bool> ValidatePythonEnvironmentAsync(string pythonPath)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "-c \"import flask; print(flask.__version__)\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Flask {output.Trim()} available.");
                    return true;
                }

                Console.WriteLine($"Flask not importable: {error.Trim()}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Python validation failed: {ex.Message}");
                return false;
            }
        }
    }
}
