namespace FocusAssistant.Services.Flask.Interfaces
{
    public interface IPythonExecutableFinder
    {
        /// <summary>Path to a Python executable, or null when none could be found.</summary>
        string? FindPythonExecutable();
    }
}
