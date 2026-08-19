using FocusAssistant.Core.Intelligence;
using System.Text;

namespace FocusAssistant.Intelligence.Prompting
{
    /// <summary>
    /// Formats prompts in Phi-3's chat template.
    /// </summary>
    /// <remarks>
    /// Worth its own type and its own test because getting this wrong does not throw. The
    /// model still generates fluent text - it simply has no idea which part was the
    /// instruction and which was the data, so it drifts, echoes the prompt back, or answers
    /// a question nobody asked. That failure reads as "the model is bad" rather than as a
    /// formatting bug, which is the most expensive kind of mistake to track down.
    /// </remarks>
    public static class PhiPromptFormatter
    {
        public static string Format(LlmRequest request)
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(request.System))
                builder.Append("<|system|>\n").Append(request.System.Trim()).Append("<|end|>\n");

            builder.Append("<|user|>\n").Append(request.User.Trim()).Append("<|end|>\n");

            // The trailing assistant tag with no closing marker is what tells the model it
            // is its turn to speak.
            builder.Append("<|assistant|>\n");

            return builder.ToString();
        }

        /// <summary>
        /// Strips any template markers the model emits before the text reaches a user.
        /// </summary>
        /// <remarks>
        /// Small models leak their own control tokens often enough that this cannot be
        /// treated as an edge case - and "&lt;|end|&gt;" appearing in a daily summary makes
        /// the whole feature look broken regardless of how good the sentence was.
        /// </remarks>
        public static string Clean(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var text = raw;
            foreach (var marker in (string[])["<|end|>", "<|assistant|>", "<|user|>", "<|system|>", "<|endoftext|>"])
                text = text.Replace(marker, string.Empty);

            return TrimToLastSentence(text.Trim());
        }

        /// <summary>
        /// Drops a trailing partial sentence.
        /// </summary>
        /// <remarks>
        /// Generation stops at a token limit, not at a full stop, so output routinely ends
        /// mid-clause - "...keep up this commendable balance of productivity" with no
        /// terminator. A summary that stops mid-sentence reads as a broken feature no matter
        /// how good the preceding text was, and losing the last half-sentence costs nothing:
        /// these prompts are written so the useful content comes first.
        /// <para>
        /// Only applied when a sentence boundary exists reasonably far in. Short replies -
        /// a single fragment, a one-word answer - are returned untouched rather than
        /// trimmed to nothing.
        /// </para>
        /// </remarks>
        public static string TrimToLastSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Already ends cleanly.
            if (text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?'))
                return text;

            var lastStop = text.LastIndexOfAny(['.', '!', '?']);

            // Keep the whole thing when there is no sentence to fall back to, or when
            // trimming would discard most of the response.
            if (lastStop < 0 || lastStop < text.Length / 2)
                return text;

            return text[..(lastStop + 1)];
        }
    }
}
