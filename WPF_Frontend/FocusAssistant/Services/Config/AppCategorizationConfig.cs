using FocusAssistant.Services.Config.interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FocusAssistant.Services.Config
{
    /// <summary>
    /// Keyword rules used to categorise applications. Optionally overridden by
    /// Config/app_categories.json next to the executable; falls back to built-in
    /// defaults when that file is missing or unreadable.
    /// </summary>
    public class AppCategorizationConfig : IAppCategorizationConfig
    {
        public Dictionary<string, string[]> ProductiveApps { get; private set; }
        public Dictionary<string, string[]> DistractingApps { get; private set; }
        public string[] WorkKeywords { get; private set; }

        public AppCategorizationConfig()
        {
            // Defaults first, so a bad override can never leave these null.
            ProductiveApps = DefaultProductiveApps();
            DistractingApps = DefaultDistractingApps();
            WorkKeywords = DefaultWorkKeywords();

            TryApplyOverrides();
        }

        private void TryApplyOverrides()
        {
            var configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Config", "app_categories.json");

            if (!File.Exists(configPath))
                return;

            try
            {
                var overrides = JsonSerializer.Deserialize<CategoryOverrides>(
                    File.ReadAllText(configPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (overrides is null)
                    return;

                // Only replace what the file actually supplies.
                if (overrides.ProductiveApps is { Count: > 0 }) ProductiveApps = overrides.ProductiveApps;
                if (overrides.DistractingApps is { Count: > 0 }) DistractingApps = overrides.DistractingApps;
                if (overrides.WorkKeywords is { Length: > 0 }) WorkKeywords = overrides.WorkKeywords;
            }
            catch (Exception ex)
            {
                // Keep the defaults rather than starting up with no rules at all.
                Console.WriteLine($"Ignoring unreadable {configPath}: {ex.Message}");
            }
        }

        private sealed class CategoryOverrides
        {
            public Dictionary<string, string[]>? ProductiveApps { get; set; }
            public Dictionary<string, string[]>? DistractingApps { get; set; }
            public string[]? WorkKeywords { get; set; }
        }

        private static Dictionary<string, string[]> DefaultProductiveApps() => new()
        {
            ["Development"] = new[]
            {
                "devenv", "code", "rider", "pycharm", "intellij", "webstorm", "clion",
                "eclipse", "atom", "sublime_text", "notepad++", "vim", "gvim",
                "windowsterminal", "powershell", "pwsh", "cmd", "wt", "docker"
            },
            ["Communication"] = new[]
            {
                "outlook", "teams", "slack", "discord", "zoom", "skype", "telegram", "webex"
            },
            ["Office"] = new[]
            {
                "winword", "word", "excel", "powerpnt", "powerpoint", "onenote",
                "notion", "obsidian", "acrord32"
            },
            ["Design"] = new[]
            {
                "photoshop", "illustrator", "figma", "sketch", "canva", "blender", "inkscape"
            }
        };

        private static Dictionary<string, string[]> DefaultDistractingApps() => new()
        {
            // Browsers are ambiguous by nature: treated as distracting unless the
            // window title matches WorkKeywords.
            ["Web Browser"] = new[] { "chrome", "firefox", "msedge", "safari", "opera", "brave" },
            ["Entertainment"] = new[]
            {
                "spotify", "vlc", "netflix", "steam", "epicgameslauncher",
                "battle.net", "riotclientux", "obs64"
            },
            ["Social"] = new[] { "whatsapp", "instagram", "tiktok", "snapchat" }
        };

        private static string[] DefaultWorkKeywords() => new[]
        {
            "work", "project", "jira", "confluence", "github", "gitlab", "bitbucket",
            "stack overflow", "stackoverflow", "docs", "documentation", "pull request",
            "issue", "ticket", "localhost", "api", "azure", "aws console", "figma",
            "sheet", "spreadsheet", "meeting", "calendar", "mail"
        };
    }
}
