using FocusAssistant.Intelligence.Classification;
using FocusAssistant.Intelligence.Embeddings;
using System.Text.Json;

namespace IntelligenceSmokeTest;

/// <summary>
/// Prints the raw similarity landscape for sample titles, so the abstention thresholds
/// can be set from measurements rather than from guesswork.
/// </summary>
internal static class Diagnose
{
    public static async Task RunAsync(MiniLmEmbeddingGenerator embedder)
    {
        var labels = Path.Combine(AppContext.BaseDirectory, "Assets", "focus_labels.json");
        var file = JsonSerializer.Deserialize<LabelPrototypeFile>(await File.ReadAllTextAsync(labels))!;
        foreach (var p in file.Labels)
            p.Embedding = await embedder.EmbedAsync(p.Text);

        (string app, string title)[] samples =
        [
            ("chrome", "Stack Overflow - How to await inside a lock"),
            ("obsidian", "Meeting notes - Q3 planning"),
            ("figma", "Checkout flow redesign - Frame 12"),
            ("postman", "GET /api/v2/users - Runner"),
            ("chrome", "r/aww - cute puppies compilation"),
            ("steam", "Steam Store - Autumn Sale"),
        ];

        Console.WriteLine("\n-- similarity landscape --");
        foreach (var (app, title) in samples)
        {
            var text = ActivityTextBuilder.Build(app, title);
            var v = await embedder.EmbedAsync(text);

            var scored = file.Labels
                .Select(p => (p, s: MiniLmEmbeddingGenerator.CosineSimilarity(v, p.Embedding!)))
                .OrderByDescending(x => x.s)
                .ToList();

            var bestProd = scored.First(x => x.p.IsProductive);
            var bestDist = scored.First(x => !x.p.IsProductive);

            Console.WriteLine($"\n\"{text}\"");
            Console.WriteLine($"   top1      {scored[0].s:F3}  [{(scored[0].p.IsProductive ? "P" : "D")}] {scored[0].p.Text}");
            Console.WriteLine($"   top2      {scored[1].s:F3}  [{(scored[1].p.IsProductive ? "P" : "D")}] {scored[1].p.Text}");
            Console.WriteLine($"   best P    {bestProd.s:F3}  {bestProd.p.Text}");
            Console.WriteLine($"   best D    {bestDist.s:F3}  {bestDist.p.Text}");
            Console.WriteLine($"   margin    {Math.Abs(bestProd.s - bestDist.s):F3}");
        }
    }
}
