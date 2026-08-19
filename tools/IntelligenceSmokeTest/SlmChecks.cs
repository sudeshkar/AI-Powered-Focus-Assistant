using FocusAssistant.Core.Intelligence;
using FocusAssistant.Intelligence.Prompting;
using FocusAssistant.Intelligence.Slm;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace IntelligenceSmokeTest;

/// <summary>
/// Downloads the language model if needed, then proves it loads and generates.
/// Run with the "slm" argument; skipped otherwise, because 2.78GB is not something to
/// pull on every test run.
/// </summary>
internal static class SlmChecks
{
    public static async Task<int> RunAsync()
    {
        using var loggerFactory = LoggerFactory.Create(b => b
            .SetMinimumLevel(LogLevel.Information)
            .AddSimpleConsole(o => o.SingleLine = true));

        var modelDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FocusAssistant", "Models", "phi-3.5-mini-int4");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        var provisioner = new HuggingFaceModelProvisioner(
            http,
            loggerFactory.CreateLogger<HuggingFaceModelProvisioner>(),
            Path.Combine(AppContext.BaseDirectory, "Assets", "phi35_manifest.json"),
            modelDir);

        Console.WriteLine($"\n-- language model --");
        Console.WriteLine($"target      {modelDir}");
        Console.WriteLine($"size        {provisioner.EstimatedBytes / (1024.0 * 1024 * 1024):F2} GB");
        Console.WriteLine($"downloaded  {provisioner.IsDownloaded}");

        var failures = 0;

        if (!provisioner.IsDownloaded)
        {
            Console.WriteLine("\nDownloading (resumable - safe to interrupt)...");

            var lastPercent = -1;
            var progress = new Progress<ModelDownloadProgress>(p =>
            {
                var percent = (int)(p.Fraction * 100);
                if (percent == lastPercent) return;
                lastPercent = percent;
                Console.Write($"\r  {percent,3}%  {p.BytesReceived / (1024.0 * 1024 * 1024):F2} / " +
                              $"{p.BytesTotal / (1024.0 * 1024 * 1024):F2} GB   file {p.FileIndex}/{p.FileCount}   " +
                              $"{p.Elapsed:hh\\:mm\\:ss}   ");
            });

            var ok = await provisioner.EnsureDownloadedAsync(progress);
            Console.WriteLine();
            if (!ok)
            {
                Console.WriteLine("FAIL  download did not complete");
                return 1;
            }
        }

        Console.WriteLine($"{(provisioner.IsDownloaded ? "PASS" : "FAIL")}  all files present and correctly sized");
        if (!provisioner.IsDownloaded) return 1;

        // The prompt template is worth asserting on its own: getting it wrong produces
        // fluent nonsense rather than an error.
        var formatted = PhiPromptFormatter.Format(new LlmRequest("Be terse.", "Say OK."));
        var templateOk = formatted.Contains("<|system|>") && formatted.Contains("<|user|>")
                         && formatted.TrimEnd().EndsWith("<|assistant|>");
        if (!templateOk) failures++;
        Console.WriteLine($"{(templateOk ? "PASS" : "FAIL")}  Phi-3 chat template well formed");

        using var model = new PhiLocalLanguageModel(
            provisioner,
            loggerFactory.CreateLogger<PhiLocalLanguageModel>(),
            modelDir,
            TimeSpan.FromMinutes(10));

        Console.WriteLine("\nLoading model (expect several seconds on CPU INT4)...");
        var sw = Stopwatch.StartNew();

        var reply = await model.GenerateAsync(new LlmRequest(
            System: "You are terse. Reply with exactly the word requested and nothing else.",
            User: "Reply with exactly: OK",
            MaxNewTokens: 10));

        sw.Stop();

        var generated = !string.IsNullOrWhiteSpace(reply);
        if (!generated) failures++;
        Console.WriteLine($"{(generated ? "PASS" : "FAIL")}  first generation in {sw.Elapsed.TotalSeconds:F1}s " +
                          $"(includes model load)");
        Console.WriteLine($"        reply: \"{reply}\"");

        // A realistic prompt, and a throughput measurement, since every design decision
        // above this depends on how slow CPU INT4 actually is.
        Console.WriteLine("\nGenerating a daily insight (the real use case)...");
        sw.Restart();
        var insight = await model.GenerateAsync(new LlmRequest(
            System: "You write one short, factual paragraph about someone's focus for the day. " +
                    "Use only the numbers given. Never invent details. Be encouraging, never scolding.",
            User: "Productive time: 4h 10m. Distracted: 1h 05m. Top apps: Visual Studio Code 3h, " +
                  "Chrome 1h 20m, Slack 40m. Longest unbroken stretch: 52 minutes. Nudges shown: 2, acted on: 1.",
            MaxNewTokens: 120));
        sw.Stop();

        var wrote = !string.IsNullOrWhiteSpace(insight);
        if (!wrote) failures++;
        var words = insight?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        Console.WriteLine($"{(wrote ? "PASS" : "FAIL")}  {sw.Elapsed.TotalSeconds:F1}s for ~{words} words " +
                          $"({words / Math.Max(sw.Elapsed.TotalSeconds, 0.01):F1} words/sec)");
        Console.WriteLine($"\n{insight}\n");

        // Generation stops on a token limit, not a full stop, so without trimming this
        // routinely ends mid-clause - which reads as a broken feature.
        var endsCleanly = insight is not null &&
                          (insight.EndsWith('.') || insight.EndsWith('!') || insight.EndsWith('?'));
        if (!endsCleanly) failures++;
        Console.WriteLine($"{(endsCleanly ? "PASS" : "FAIL")}  output ends on a complete sentence");

        Console.WriteLine(failures == 0 ? "Language model checks passed." : $"{failures} check(s) FAILED.");
        return failures;
    }
}
