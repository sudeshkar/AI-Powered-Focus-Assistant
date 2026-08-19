using FocusAssistant.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;

namespace FocusAssistant.Appearance
{
    /// <summary>
    /// The one setting in the app a user can change that has nowhere to live: everything
    /// else in Settings either reads appsettings.json or acts immediately without needing to
    /// be remembered (pause, delete). A theme choice has to survive a restart or picking
    /// "Light" would silently revert to "System" every time the app reopens.
    /// </summary>
    public sealed class ThemePreferenceStore
    {
        private readonly string _filePath = Path.Combine(AppPaths.DataDirectory, "theme.json");
        private readonly ILogger<ThemePreferenceStore> _logger;

        public ThemePreferenceStore(ILogger<ThemePreferenceStore> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AppThemePreference Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return AppThemePreference.System;

                var json = File.ReadAllText(_filePath);
                var saved = JsonSerializer.Deserialize<SavedPreference>(json);
                return saved is not null && Enum.IsDefined(typeof(AppThemePreference), saved.Theme)
                    ? saved.Theme
                    : AppThemePreference.System;
            }
            catch (Exception ex)
            {
                // A corrupt or unreadable preference file is not worth failing startup over -
                // System is the same default a first run would get anyway.
                _logger.LogDebug(ex, "Could not read the saved theme preference; defaulting to System");
                return AppThemePreference.System;
            }
        }

        public void Save(AppThemePreference theme)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                File.WriteAllText(_filePath, JsonSerializer.Serialize(new SavedPreference(theme)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not save the theme preference");
            }
        }

        private sealed record SavedPreference(AppThemePreference Theme);
    }
}
