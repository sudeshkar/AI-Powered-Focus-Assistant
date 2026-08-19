using System;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FocusAssistant.Appearance
{
    /// <summary>
    /// Owns the one live window the theme actually applies to, so both startup
    /// (<see cref="AttachAndApply"/>) and a runtime toggle in Settings
    /// (<see cref="SetPreference"/>) go through the same code path instead of duplicating
    /// the System/Light/Dark branching in two places.
    /// </summary>
    public sealed class ThemeService
    {
        private readonly ThemePreferenceStore _store;
        private Window? _window;

        public ThemeService(ThemePreferenceStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Current = _store.Load();
        }

        public AppThemePreference Current { get; private set; }

        /// <summary>Called once, from MainWindow's constructor.</summary>
        public void AttachAndApply(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            Apply(Current);
        }

        /// <summary>Called from the Settings toggle. Persists the choice and applies it live.</summary>
        public void SetPreference(AppThemePreference preference)
        {
            Current = preference;
            _store.Save(preference);
            Apply(preference);
        }

        private void Apply(AppThemePreference preference)
        {
            if (_window is null)
                return;

            if (preference == AppThemePreference.System)
            {
                // Watch's own initial paint proved unreliable in practice - it would attach
                // the live-update hook correctly but not always apply the current system
                // theme at the moment it starts watching, so the first paint stayed on
                // whatever ApplicationTheme the app happens to default to (Light) even when
                // Windows was already in Dark. Resolving and applying it explicitly first
                // means the initial paint no longer depends on Watch's own detection timing
                // - Watch is then only responsible for catching *future* OS changes.
                var systemTheme = ApplicationThemeManager.GetSystemTheme();
                var resolved = systemTheme == SystemTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
                ApplicationThemeManager.Apply(resolved, WindowBackdropType.Mica, updateAccent: true);

                // Re-entrant safe: Watch on an already-watched window just re-attaches.
                SystemThemeWatcher.Watch(_window, WindowBackdropType.Mica, updateAccents: true);
                return;
            }

            // Watch keeps listening for OS changes until told to stop - switching to an
            // explicit Light/Dark choice must unhook it, or the next OS theme change would
            // silently overwrite what the user just picked.
            SystemThemeWatcher.UnWatch(_window);

            var theme = preference == AppThemePreference.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: true);
        }
    }
}
