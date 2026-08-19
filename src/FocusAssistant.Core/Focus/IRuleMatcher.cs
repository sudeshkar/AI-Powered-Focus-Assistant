namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// The keyword ruleset, reporting what it matched rather than a yes/no.
    /// </summary>
    /// <remarks>
    /// Implemented by the existing RuleBasedProductivityStrategy. Its logic was never the
    /// problem - explicit keyword hits are high precision and stay authoritative. The
    /// problem was that it had no way to say "no idea", so the layer that could answer
    /// never got asked.
    /// </remarks>
    public interface IRuleMatcher
    {
        RuleMatch Match(string? appName, string? windowTitle);
    }
}
