namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// An override store with nothing in it.
    /// </summary>
    /// <remarks>
    /// Placeholder until the SQLite-backed store and the "This is work" button that feeds
    /// it exist. Registered rather than left null so the layered classifier can be written
    /// once, with the override layer always present - the difference between having
    /// corrections and not having them is a registration, not a code path.
    /// </remarks>
    public sealed class NoUserOverrideStore : IUserOverrideStore
    {
        public bool? Match(string? appName, string? windowTitle) => null;
    }
}
