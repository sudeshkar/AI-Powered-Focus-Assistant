namespace FocusAssistant.Core.Intelligence
{
    /// <summary>
    /// What state the local language model is in.
    /// </summary>
    /// <remarks>
    /// Exposed to the UI because a 2.5GB download is not something to hide behind a
    /// spinner. Every state here is one the user may sit in for a long time, including
    /// <see cref="NotDownloaded"/>, which is the state the app is designed to work in
    /// indefinitely.
    /// </remarks>
    public enum ModelAvailability
    {
        /// <summary>Never fetched. The app is fully functional in this state.</summary>
        NotDownloaded,

        Downloading,

        /// <summary>On disk, being loaded into memory. Takes seconds, not milliseconds.</summary>
        Loading,

        Ready,

        /// <summary>Download or load failed. The app carries on without it.</summary>
        Failed,

        /// <summary>Turned off in settings.</summary>
        Disabled,
    }
}
