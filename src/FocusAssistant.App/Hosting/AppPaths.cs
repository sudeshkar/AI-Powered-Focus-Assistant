using System;
using System.IO;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Where the app keeps user-machine-local state.
    /// </summary>
    /// <remarks>
    /// These paths were duplicated between App.xaml.cs and the DbContext's design-time
    /// fallback, so the two could drift and point at different files. LocalApplicationData
    /// rather than Roaming: the database can reach hundreds of megabytes and there is no
    /// reason to sync a machine's activity log into a domain profile.
    /// </remarks>
    public static class AppPaths
    {
        public static string DataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FocusAssistant");

        public static string DatabasePath { get; } = Path.Combine(DataDirectory, "focusassistant.db");

        public static string LogDirectory { get; } = Path.Combine(DataDirectory, "Logs");

        /// <summary>Root for downloaded models. The committed embedding model does not live here.</summary>
        public static string ModelDirectory { get; } = Path.Combine(DataDirectory, "Models");
    }
}
