using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace FocusAssistant.Platform.Startup
{
    /// <summary>Turns "start with Windows" on and off.</summary>
    public interface IAutoStartService
    {
        bool IsEnabled { get; }
        void Enable();
        void Disable();
    }

    /// <summary>
    /// Registers the app under the current user's Run key.
    /// </summary>
    /// <remarks>
    /// HKCU rather than HKLM, and a Run entry rather than a scheduled task: both of the
    /// alternatives need administrator rights, and a focus tracker that demands elevation
    /// to launch itself is asking for more trust than the feature is worth. The Run key is
    /// also the one place users know to look when they want to undo this.
    /// </remarks>
    public sealed class AutoStartService : IAutoStartService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "FocusAssistant";

        public bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue(ValueName) is not null;
                }
                catch (Exception)
                {
                    // A locked-down or corrupt registry means the feature is unavailable,
                    // not that the app should fail.
                    return false;
                }
            }
        }

        public void Enable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null)
                return;

            var path = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(path))
                return;

            // Quoted: the executable normally sits under a path containing spaces, and an
            // unquoted Run value would have Windows try to launch the first word of it.
            key.SetValue(ValueName, $"\"{path}\"");
        }

        public void Disable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
