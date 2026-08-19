using System;
using System.Text.RegularExpressions;

namespace FocusAssistant.Intelligence.Classification
{
    /// <summary>
    /// Turns an application name and window title into the sentence the model sees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single highest-leverage piece of the classifier. The model is fixed and the
    /// prototypes are a config file, but what gets embedded is decided here - and raw
    /// window titles are mostly noise. "(3) Slack | general | Acme" and
    /// "index.ts - myapp - Visual Studio Code" both carry one useful signal buried in
    /// punctuation, an unread count, and an application name repeated from the field
    /// beside it.
    /// </para>
    /// <para>
    /// Everything stripped here is something that makes two genuinely similar activities
    /// look different to a cosine metric: unread badges change every minute, the browser
    /// suffix is identical across every tab and so pulls all of them together, and file
    /// paths add tokens that mean nothing to a sentence model.
    /// </para>
    /// </remarks>
    public static partial class ActivityTextBuilder
    {
        /// <summary>
        /// Trailing application-name suffixes browsers and editors append to every window.
        /// They are constant across wildly different activities, so they act as a common
        /// term dragging unrelated titles toward each other.
        /// </summary>
        [GeneratedRegex(
            @"\s*[-—|]\s*(Google Chrome|Mozilla Firefox|Microsoft.?\s?Edge|Brave|Opera|Vivaldi|Chromium|Safari|Visual Studio Code|Visual Studio|IntelliJ IDEA|PyCharm|WebStorm|Rider|Sublime Text|Notepad\+\+)\s*$",
            RegexOptions.IgnoreCase)]
        private static partial Regex AppSuffix();

        /// <summary>Unread/notification counts: "(3) Slack", "[5] Gmail".</summary>
        [GeneratedRegex(@"^\s*[\(\[]\d+[\)\]]\s*")]
        private static partial Regex UnreadCount();

        /// <summary>A leading asterisk or bullet marking unsaved changes.</summary>
        [GeneratedRegex(@"^\s*[\*•]\s*")]
        private static partial Regex DirtyMarker();

        /// <summary>Windows file paths, which contribute tokens but no meaning.</summary>
        [GeneratedRegex(@"[A-Za-z]:\\[^\s]*")]
        private static partial Regex FilePath();

        [GeneratedRegex(@"\s{2,}")]
        private static partial Regex ExcessWhitespace();

        /// <summary>
        /// Roughly 64 tokens' worth of characters. The generator truncates at the token
        /// level anyway; cutting here just avoids tokenising text that will be discarded.
        /// </summary>
        private const int MaxCharacters = 300;

        public static string Build(string? appName, string? windowTitle)
        {
            var app = CleanAppName(appName);
            var title = CleanTitle(windowTitle, app);

            var text = string.IsNullOrWhiteSpace(title) ? app : $"{app}: {title}";

            if (text.Length > MaxCharacters)
                text = text[..MaxCharacters];

            return text;
        }

        private static string CleanAppName(string? appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return "unknown application";

            var name = appName.Trim();

            // Process names arrive as executables; ".exe" is a token that appears in every
            // single input and therefore distinguishes nothing.
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            return name.Replace('_', ' ').Replace('-', ' ').Trim();
        }

        private static string CleanTitle(string? windowTitle, string appName)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return string.Empty;

            var title = windowTitle.Trim();

            title = UnreadCount().Replace(title, string.Empty);
            title = DirtyMarker().Replace(title, string.Empty);
            title = AppSuffix().Replace(title, string.Empty);
            title = FilePath().Replace(title, string.Empty);
            title = ExcessWhitespace().Replace(title, " ").Trim(' ', '-', '—', '|', ':');

            // The title often ends with the application's own name; it is already the other
            // half of the sentence, so repeating it just weights that token twice.
            if (title.Equals(appName, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return title;
        }
    }
}
