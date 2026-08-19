using FocusAssistant.Core.Config;
using FocusAssistant.Core.Focus;
using FocusAssistant.Intelligence.Classification;
using FocusAssistant.Intelligence.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntelligenceSmokeTest;

/// <summary>
/// End-to-end checks on the full layer stack, with the real keyword ruleset in front of
/// the real model - the arrangement the running app actually uses.
/// </summary>
internal static class LayeredChecks
{
    public static async Task<int> RunAsync(MiniLmEmbeddingGenerator embedder)
    {
        var semantic = new EmbeddingSemanticClassifier(
            embedder, NullLogger<EmbeddingSemanticClassifier>.Instance, 0.12, 0.06);
        await semantic.WarmUpAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "focus_labels.json"));

        var rules = new RuleBasedProductivityStrategy(
            new AppCategorizationConfig(NullLogger<AppCategorizationConfig>.Instance));

        var classifier = new LayeredActivityClassifier(
            rules, semantic, new NullGoalRelevanceScorer(), new NoUserOverrideStore());

        Console.WriteLine("\n-- full stack: rules + model --\n");

        var failures = 0;

        async Task Expect(string app, string title, bool wantProductive, ClassificationSource wantSource)
        {
            var context = new ActivityContext(app, title, rules.GetCategory(app), DateTimeOffset.Now, null);
            var verdict = await classifier.ClassifyAsync(context);

            var ok = verdict.IsProductive == wantProductive && verdict.Source == wantSource;
            if (!ok) failures++;

            Console.WriteLine(
                $"{(ok ? "PASS" : "FAIL")}  {(verdict.IsProductive ? "productive " : "distracting")} " +
                $"via {verdict.Source,-13} c={verdict.Confidence:F2}  {verdict.Rationale}");
            Console.WriteLine($"        {app} | {title}");
            if (!ok)
                Console.WriteLine($"        wanted {(wantProductive ? "productive" : "distracting")} via {wantSource}");
        }

        // A known editor: the rule settles it and the model is never consulted.
        await Expect("code", "Program.cs - myapp - Visual Studio Code", true, ClassificationSource.Rule);

        // Browsers are the case this layering exists for. The process name is identical
        // across all of these; only the title differs, and only the model reads it well.
        await Expect("chrome", "r/aww - cute puppies compilation", false, ClassificationSource.Embedding);
        await Expect("chrome", "Amazon.com - Wireless headphones", false, ClassificationSource.Embedding);
        await Expect("chrome", "Grafana - Service latency dashboard", true, ClassificationSource.Embedding);
        // The model abstains on this one - "Figma" alone is thin - and the advisory browser
        // rule catches it instead. Asserted as Rule deliberately: the fallback chain having
        // a second chance is the behaviour worth pinning down, not an accident to paper over.
        await Expect("chrome", "Figma - Checkout flow redesign", true, ClassificationSource.Rule);

        // steam is in the Entertainment keyword list, so the rule is authoritative and the
        // model is not consulted. Included to prove ambiguity is scoped to browsers rather
        // than applied to every distracting application.
        await Expect("steam", "Steam Store - Autumn Sale", false, ClassificationSource.Rule);

        // An application no list mentions: previously assumed productive with no evidence.
        await Expect("postman", "GET /api/v2/users - Runner", true, ClassificationSource.Embedding);

        Console.WriteLine(failures == 0
            ? "\nLayered checks passed."
            : $"\n{failures} layered check(s) FAILED.");

        return failures;
    }
}
