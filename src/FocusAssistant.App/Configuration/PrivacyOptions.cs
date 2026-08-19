using System;

namespace FocusAssistant.Configuration
{
    /// <summary>How much of what the user does is written down, and for how long.</summary>
    public sealed class PrivacyOptions
    {
        public const string SectionName = "Privacy";

        /// <summary>Days of per-application detail to keep. Daily aggregates outlive this.</summary>
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
        public string[] ExcludedProcesses { get; set; } =
        [
            "1Password", "Bitwarden", "KeePass", "KeePassXC", "Dashlane", "LastPass",
        ];
    }

    public enum TitleCaptureMode
    {
        /// <summary>Store the title verbatim.</summary>
        Full,

        /// <summary>Store only the application name.</summary>
        AppOnly,

        /// <summary>Store the classifier's category and a hash, never the text.</summary>
        Redacted,
    }
}
