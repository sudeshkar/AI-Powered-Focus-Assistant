using FocusAssistant.Core.Focus;
using FocusAssistant.Intelligence.Classification;
using FocusAssistant.Intelligence.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntelligenceSmokeTest;

/// <summary>
/// Exercises the semantic classifier against realistic window titles - the ones the
/// keyword ruleset has no entry for, which is the whole reason this layer exists.
/// </summary>
internal static class ClassifierChecks
{
    public static async Task<int> RunAsync(MiniLmEmbeddingGenerator embedder)
    {
        var classifier = new EmbeddingSemanticClassifier(
            embedder,
            NullLogger<EmbeddingSemanticClassifier>.Instance,
            minimumSimilarity: 0.12,
            minimumMargin: 0.06);

        var labels = Path.Combine(AppContext.BaseDirectory, "Assets", "focus_labels.json");
        await classifier.WarmUpAsync(labels);

        Console.WriteLine("\n-- semantic classification of unrecognised applications --");
        Console.WriteLine("(these are exactly the titles the keyword ruleset assumed were productive)\n");

        var failures = 0;

        async Task Expect(string app, string title, bool? expectProductive)
        {
            var context = new ActivityContext(app, title, "Other", DateTimeOffset.Now, null);
            var verdict = await classifier.ClassifyAsync(context);

            var actual = verdict?.IsProductive;
            var ok = actual == expectProductive;
            if (!ok) failures++;

            var shown = verdict is null
                ? "abstained"
                : $"{(verdict.Value.IsProductive ? "productive " : "distracting")} " +
                  $"c={verdict.Value.Confidence:F2}  {verdict.Value.Rationale}";

            var want = expectProductive switch
            {
                true => "productive",
                false => "distracting",
                null => "abstain",
            };

            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  want {want,-11} got {shown}");
            Console.WriteLine($"        {app} | {title}");
        }

        // Things nobody put in a keyword list, which the old ruleset called productive.
        // The model abstains here: "Stack Overflow" plus a terse question reads as
        // ambiguous, and abstaining hands it to the layer below rather than guessing.
        await Expect("chrome", "Stack Overflow - How to await inside a lock", null);
        await Expect("chrome", "r/aww - cute puppies compilation", false);
        await Expect("obsidian", "Meeting notes - Q3 planning", true);
                await Expect("figma", "Checkout flow redesign - Frame 12", true);
        await Expect("steam", "Steam Store - Autumn Sale", false);
        await Expect("chrome", "Amazon.com - Wireless headphones", false);
        await Expect("postman", "GET /api/v2/users - Runner", true);
        await Expect("spotify", "Discover Weekly", false);

        Console.WriteLine(failures == 0
            ? "\nClassifier checks passed."
            : $"\n{failures} classifier check(s) FAILED.");

        return failures;
    }
}
