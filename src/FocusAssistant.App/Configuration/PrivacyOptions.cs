using FocusAssistant.Core.Privacy;

namespace FocusAssistant.Configuration
{
    /// <summary>How much of what the user does is written down, and for how long.</summary>
    public sealed class PrivacyOptions
    {
        public const string SectionName = "Privacy";

        /// <summary>Days of per-application detail to keep. Session-level aggregates outlive this.</summary>
        public int RetentionDays { get; set; } = 90;

        /// <summary>
        /// Window titles carry document names, email subjects, and client names.
        /// <see cref="TitleCaptureMode.AppOnly"/> drops them entirely; the layered
        /// classifier degrades to app-name-only rather than breaking.
        /// </summary>
        public TitleCaptureMode TitleCapture { get; set; } = TitleCaptureMode.Full;

        /// <summary>
        /// Processes whose activity is recorded as an opaque block with a duration and
        /// nothing else - password managers and banking sites have no business in a
        /// productivity log.
        /// </summary>
        /// <remarks>
        /// Defaults to empty here, not to the actual default list, because
        /// Microsoft.Extensions.Configuration's array binding appends config values onto
        /// whatever the property already holds rather than replacing it - a property
        /// initialised with six names plus six more from appsettings.json produces twelve,
        /// six of them duplicates. The real defaults live in appsettings.json (already
        /// checked in), which is also the file people actually edit; a C# fallback here
        /// would just be a second place for the two to quietly disagree.
        /// </remarks>
        public string[] ExcludedProcesses { get; set; } = [];
    }
}
