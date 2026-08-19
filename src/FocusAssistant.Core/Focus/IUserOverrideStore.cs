namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Corrections the user made, which outrank every model and every rule.
    /// </summary>
    /// <remarks>
    /// The entire learning loop, and deliberately the dullest possible mechanism: a
    /// correction the user made, applied deterministically, visible and editable in
    /// Settings. No opaque model update, and nothing they cannot undo.
    /// </remarks>
    public interface IUserOverrideStore
    {
        /// <summary>
        /// True or false when the user has ruled on this app and title, null when they have
        /// not. Must be cheap - this is consulted on the hot path.
        /// </summary>
        bool? Match(string? appName, string? windowTitle);
    }
}
