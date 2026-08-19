namespace FocusAssistant.Core.Intelligence
{
    /// <summary>
    /// One generation request.
    /// </summary>
    /// <param name="System">Instructions defining the model's role and limits.</param>
    /// <param name="User">The actual prompt.</param>
    /// <param name="MaxNewTokens">
    /// Hard ceiling on output length. Low by default: on CPU INT4 this model produces
    /// roughly 5-20 tokens a second, so 160 tokens is already ten seconds of work. Every
    /// prompt in this app is written to need a sentence or two, not an essay.
    /// </param>
    /// <param name="Temperature">
    /// Low by default. These outputs are shown as statements about the user's own day, and
    /// a model inventing a livelier version of their afternoon is worse than a dull
    /// accurate one.
    /// </param>
    public sealed record LlmRequest(
        string System,
        string User,
        int MaxNewTokens = 160,
        float Temperature = 0.4f);
}
