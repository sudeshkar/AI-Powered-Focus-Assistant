namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Decides whether a given application/window counts as productive.
    /// </summary>
    public interface IProductivityStrategy
    {
        /// <summary>True when the activity should count towards productive time.</summary>
        bool IsProductive(string? appName, string? windowTitle);

        /// <summary>Coarse category for the application, e.g. "Development"; "Other" when unknown.</summary>
        string GetCategory(string? appName);
    }
}
