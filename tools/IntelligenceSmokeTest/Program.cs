using FocusAssistant.Intelligence.Embeddings;
using System.Diagnostics;

// Proves the embedding stack actually works on this machine, rather than merely
// compiling. The similarity assertions are the point: a tokenizer pointed at the wrong
// vocabulary file still returns vectors, it just returns meaningless ones, and every
// number below collapses toward the middle when that happens.

// The language model is 2.78GB, so it is opt-in: pass "slm" to exercise it.
if (args.Contains("slm"))
    return await IntelligenceSmokeTest.SlmChecks.RunAsync() == 0 ? 0 : 1;

var modelDir = Path.Combine(AppContext.BaseDirectory, "Models", "minilm");
Console.WriteLine($"Loading model from {modelDir}");

var sw = Stopwatch.StartNew();
using var embedder = MiniLmEmbeddingGenerator.Load(modelDir);
Console.WriteLine($"Loaded in {sw.ElapsedMilliseconds} ms\n");

async Task<double> Sim(string a, string b)
{
    var va = await embedder.EmbedAsync(a);
    var vb = await embedder.EmbedAsync(b);
    return MiniLmEmbeddingGenerator.CosineSimilarity(va, vb);
}

var failures = 0;

async Task Check(string a, string b, string op, double bound)
{
    var score = await Sim(a, b);
    var ok = op == ">" ? score > bound : score < bound;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {score:F3} {op} {bound:F2}   \"{a}\" vs \"{b}\"");
}

Console.WriteLine("-- identical text must be exactly similar to itself --");
await Check("visual studio code", "visual studio code", ">", 0.999);

Console.WriteLine("\n-- related pairs should score high --");
// Calibrated against what this checkpoint actually produces. MiniLM paraphrase scores
// sit around 0.5-0.7, not near 1.0; what the classifier relies on is the gap between
// these and the unrelated pairs below, which is roughly 0.4 and very comfortable.
await Check("writing code", "programming in an editor", ">", 0.55);
await Check("reading api documentation", "browsing technical reference docs", ">", 0.40);

Console.WriteLine("\n-- unrelated pairs should score low --");
await Check("writing code", "watching netflix", "<", 0.40);
await Check("reviewing a pull request", "scrolling a social media feed", "<", 0.40);

Console.WriteLine("\n-- a vector must be unit length --");
var v = await embedder.EmbedAsync("visual studio code - Program.cs");
var magnitude = Math.Sqrt(v.Sum(x => (double)x * x));
var unit = Math.Abs(magnitude - 1.0) < 1e-4;
if (!unit) failures++;
Console.WriteLine($"{(unit ? "PASS" : "FAIL")}  |v| = {magnitude:F6} (expected 1.0), dimensions = {v.Length}");

Console.WriteLine("\n-- latency, the budget the hot path depends on --");
await embedder.EmbedAsync("warm up");
var timer = Stopwatch.StartNew();
const int iterations = 50;
for (var i = 0; i < iterations; i++)
    await embedder.EmbedAsync($"Visual Studio Code - MainWindow.xaml.cs - iteration {i}");
timer.Stop();
var perCall = timer.Elapsed.TotalMilliseconds / iterations;
var fastEnough = perCall < 15;
if (!fastEnough) failures++;
Console.WriteLine($"{(fastEnough ? "PASS" : "FAIL")}  {perCall:F2} ms per embedding (budget 15 ms)");

failures += await IntelligenceSmokeTest.ClassifierChecks.RunAsync(embedder);
failures += await IntelligenceSmokeTest.LayeredChecks.RunAsync(embedder);

Console.WriteLine(failures == 0
    ? "\nAll checks passed."
    : $"\n{failures} check(s) FAILED.");

return failures == 0 ? 0 : 1;
