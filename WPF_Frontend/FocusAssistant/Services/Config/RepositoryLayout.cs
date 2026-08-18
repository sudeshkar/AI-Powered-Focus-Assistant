using System;
using System.IO;

namespace FocusAssistant.Services.Config
{
    /// <summary>
    /// Locates the repository root and the Python backend inside it.
    /// </summary>
    /// <remarks>
    /// This used to be duplicated in FlaskConfiguration and PythonExecutableFinder as
    /// "walk up three levels from AppDomain.BaseDirectory", which lands on the csproj
    /// directory rather than the repository root, so every derived path was wrong.
    /// Searching upward for a known marker directory works from bin\Debug\net8.0-*,
    /// from a publish folder, and from the test host alike.
    /// </remarks>
    public static class RepositoryLayout
    {
        private const string BackendFolderName = "Python_Backend";
        private const int MaxLevelsToSearch = 8;

        private static readonly Lazy<string?> _backendDirectory = new(FindBackendDirectory);

        /// <summary>Full path to Python_Backend, or null when it cannot be found.</summary>
        public static string? BackendDirectory => _backendDirectory.Value;

        /// <summary>Full path to the Flask entry point, or null when it cannot be found.</summary>
        public static string? BackendScriptPath
        {
            get
            {
                var dir = BackendDirectory;
                if (dir is null) return null;

                var script = Path.Combine(dir, "app.py");
                return File.Exists(script) ? script : null;
            }
        }

        private static string? FindBackendDirectory()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            for (var level = 0; level < MaxLevelsToSearch && current is not null; level++)
            {
                var candidate = Path.Combine(current.FullName, BackendFolderName);
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "app.py")))
                    return candidate;

                current = current.Parent;
            }

            Console.WriteLine(
                $"Could not locate {BackendFolderName} above {AppContext.BaseDirectory}. " +
                "The AI backend will be unavailable.");
            return null;
        }
    }
}
