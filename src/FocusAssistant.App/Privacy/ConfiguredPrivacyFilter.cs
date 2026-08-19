using FocusAssistant.Configuration;
using FocusAssistant.Core.Privacy;
using Microsoft.Extensions.Options;
using System;
using System.Linq;

namespace FocusAssistant.Privacy
{
    /// <summary>
    /// Applies <see cref="PrivacyOptions"/> to raw activity before it is stored.
    /// </summary>
    /// <remarks>
    /// Takes <see cref="IOptionsMonitor{TOptions}"/> rather than a snapshot, so a change to
    /// <c>appsettings.json</c> (title capture is set to reload on change) or a future
    /// Settings-screen toggle takes effect on the very next window switch, not after a
    /// restart.
    /// </remarks>
    public sealed class ConfiguredPrivacyFilter : IActivityPrivacyFilter
    {
        /// <summary>What an excluded process's activity is called in the log.</summary>
        public const string PrivatePlaceholder = "Private";

        private readonly IOptionsMonitor<PrivacyOptions> _options;

        public ConfiguredPrivacyFilter(IOptionsMonitor<PrivacyOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public PrivacyDecision Apply(string appName, string? windowTitle, string category)
        {
            var options = _options.CurrentValue;

            if (IsExcluded(appName, options))
                return new PrivacyDecision(PrivatePlaceholder, null, IsExcluded: true);

            var title = options.TitleCapture switch
            {
                TitleCaptureMode.Full => windowTitle,
                TitleCaptureMode.AppOnly => null,
                TitleCaptureMode.Redacted => $"[{category}]",
                _ => windowTitle,
            };

            return new PrivacyDecision(appName, title, IsExcluded: false);
        }

        private static bool IsExcluded(string appName, PrivacyOptions options) =>
            options.ExcludedProcesses.Any(excluded =>
                appName.Contains(excluded, StringComparison.OrdinalIgnoreCase));
    }
}
