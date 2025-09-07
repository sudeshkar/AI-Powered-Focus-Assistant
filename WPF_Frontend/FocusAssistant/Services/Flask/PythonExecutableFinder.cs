using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    public class PythonExecutableFinder : IPythonExecutableFinder
    {
        private readonly string _projectRoot;

        public PythonExecutableFinder()
        {
            // Start from bin directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Go up three levels to reach the project root
            _projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.FullName
                           ?? throw new Exception("Project root not found");
        }

        public string FindPythonExecutable()
        {
            // Try multiple possible locations for Python executable
            var possiblePaths = GetPossiblePythonPaths();

            foreach (var path in possiblePaths)
            {
                Console.WriteLine($"🔍 Checking Python path: {path}");
                if (File.Exists(path))
                {
                    Console.WriteLine($"✅ Found Python executable at: {path}");
                    return path;
                }
            }

            // If no virtual environment Python found, try system Python
            var systemPython = FindSystemPython();
            if (!string.IsNullOrEmpty(systemPython))
            {
                Console.WriteLine($"✅ Using system Python at: {systemPython}");
                return systemPython;
            }

            // Show all attempted paths in error message
            var attemptedPaths = string.Join("\n  - ", possiblePaths);
            throw new FileNotFoundException(
                $"Python executable not found. Attempted paths:\n  - {attemptedPaths}\n\n" +
                "Please ensure you have:\n" +
                "1. Created a virtual environment in Python_Backend/venv\n" +
                "2. Or have Python installed system-wide\n" +
                "3. The project structure matches the expected layout");
        }

        private List<string> GetPossiblePythonPaths()
        {
            return new List<string>
            {
                // Virtual environment in Python_Backend folder (relative to project root)
                Path.Combine(_projectRoot, "Python_Backend", "venv", "Scripts", "python.exe"),
                Path.Combine(_projectRoot, "Python_Backend", "venv", "bin", "python"), // Linux/Mac
                
                // Alternative project structures
                Path.Combine(_projectRoot, "..", "Python_Backend", "venv", "Scripts", "python.exe"),
                Path.Combine(_projectRoot, "..", "Python_Backend", "venv", "bin", "python"),
                
                // Based on your actual file structure from the logs
                Path.Combine("C:", "Final Project", "AI-Powered-Focus-Assistant", "Python_Backend", "venv", "Scripts", "python.exe"),
                Path.Combine("C:", "Final Project", "AI-Powered-Focus-Assistant", "Python_Backend", "venv", "bin", "python"),
                
                // Direct in bin folder (if copied there)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python_Backend", "venv", "Scripts", "python.exe"),
                
                // Portable Python in application folder
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python", "python.exe")
            };
        }

        private string? FindSystemPython()
        {
            try
            {
                // Try common system Python locations
                var systemPaths = new[]
                {
                    @"C:\Python312\python.exe",
                    @"C:\Python311\python.exe",
                    @"C:\Python310\python.exe",
                    @"C:\Python39\python.exe",
                    @"C:\Program Files\Python312\python.exe",
                    @"C:\Program Files\Python311\python.exe",
                    @"C:\Program Files\Python310\python.exe",
                    @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python312\python.exe",
                    @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python311\python.exe",
                    @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python310\python.exe"
                };

                foreach (var path in systemPaths)
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                // Try using PATH environment variable
                return FindPythonInPath();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error finding system Python: {ex.Message}");
                return null;
            }
        }

        private string? FindPythonInPath()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "python",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    // Return the first Python executable found
                    var firstLine = output.Split('\n')[0].Trim();
                    if (File.Exists(firstLine))
                    {
                        return firstLine;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error checking PATH for Python: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Validates that the Python executable can run and has required packages
        /// </summary>
        public async Task<bool> ValidatePythonEnvironmentAsync(string pythonPath)
        {
            try
            {
                // Check if Python runs
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"❌ Python validation failed: {error}");
                    return false;
                }

                Console.WriteLine($"✅ Python version: {output.Trim()}");

                // Optionally check for Flask
                return await CheckFlaskInstallation(pythonPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Python validation error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CheckFlaskInstallation(string pythonPath)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "-c \"import flask; print('Flask version:', flask.__version__)\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"✅ {output.Trim()}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"⚠️ Flask not found: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Flask check error: {ex.Message}");
                return false;
            }
        }
    }
}