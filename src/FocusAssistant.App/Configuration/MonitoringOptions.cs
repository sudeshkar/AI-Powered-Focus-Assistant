using System;

namespace FocusAssistant.Configuration
{
    /// <summary>
    /// How often the Win32 monitors poll, and what counts as a real stretch of use.
    /// </summary>
    /// <remarks>
    /// These were hardcoded in the DI factory lambdas, which meant tuning the
    /// polling behaviour required a rebuild. They are bound from appsettings.json
    /// so a slow machine can back the poll off without touching code.
    /// </remarks>
    public sealed class MonitoringOptions
    {
        public const string SectionName = "Monitoring";

        /// <summary>How often the foreground window is sampled.</summary>
        public TimeSpan WindowPollInterval { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>Input silence after which the user counts as away.</summary>
        public TimeSpan IdleThreshold { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Shortest stretch worth recording. Alt-tabbing through windows should not
        /// litter the database or raise an activity event per keystroke-driven title
        /// change.
        /// </summary>
        public TimeSpan MinimumUsageDuration { get; set; } = TimeSpan.FromSeconds(2);
    }
}
