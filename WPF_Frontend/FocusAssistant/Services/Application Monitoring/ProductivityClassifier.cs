using FocusAssistant.Services.Application_Monitoring.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Application_Monitoring
{
    public class ProductivityClassifier : IProductivityClassifier
    {
        private readonly Dictionary<string, string> _appCategories;
        private readonly HashSet<string> _productiveApps;
        private readonly HashSet<string> _distractingApps;
        private readonly string[] _workKeywords;

        public ProductivityClassifier()
        {
            _appCategories = InitializeAppCategories();
            _productiveApps = InitializeProductiveApps();
            _distractingApps = InitializeDistractingApps();
            _workKeywords = new[] { "github", "stackoverflow", "documentation", "tutorial", "course", "learning", "work", "project" };
        }
        public string CategorizeApp(string appName)
        {
            if (string.IsNullOrEmpty(appName))
                return "Unknown";

            var lowerAppName = appName.ToLower();
            return _appCategories.TryGetValue(lowerAppName, out string category) ? category : "Other";
        }

        public double GetProductivityScore(string appName, string windowTitle)
        {
            var category = CategorizeApp(appName);
            var isProductive = IsProductiveActivity(appName, windowTitle);

            return category switch
            {
                "Development" => 1.0,
                "Office" => 0.9,
                "Communication" => 0.7,
                "Browser" => isProductive ? 0.6 : 0.2,
                "Entertainment" => 0.1,
                "Games" => 0.0,
                _ => 0.5
            };
        }

        public bool IsProductiveActivity(string appName, string windowTitle)
        {
            if (string.IsNullOrEmpty(appName))
                return false;

            var lowerAppName = appName.ToLower();

            // Check if explicitly productive
            if (_productiveApps.Contains(lowerAppName))
                return true;

            // Check if explicitly distracting
            if (_distractingApps.Contains(lowerAppName))
            {
                // Even distracting apps can be work-related
                return IsWorkRelatedContent(windowTitle);
            }

            // Default: consider unknown apps as neutral/productive
            return true;
        }
        private bool IsWorkRelatedContent(string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle))
                return false;

            var lowerTitle = windowTitle.ToLower();
            return Array.Exists(_workKeywords, keyword => lowerTitle.Contains(keyword));
        }
        private Dictionary<string, string> InitializeAppCategories()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Development
                { "devenv", "Development" },
                { "code", "Development" },
                { "notepad++", "Development" },
                { "sublime_text", "Development" },
                { "atom", "Development" },
                { "pycharm", "Development" },
                { "intellij", "Development" },
                { "eclipse", "Development" },
                { "netbeans", "Development" },

                // Office
                { "winword", "Office" },
                { "excel", "Office" },
                { "powerpnt", "Office" },
                { "outlook", "Office" },

                // Communication
                { "teams", "Communication" },
                { "slack", "Communication" },
                { "zoom", "Communication" },
                { "discord", "Communication" },

                // Browsers
                { "chrome", "Browser" },
                { "firefox", "Browser" },
                { "msedge", "Browser" },
                { "safari", "Browser" },
                { "opera", "Browser" },

                // Entertainment
                { "spotify", "Entertainment" },
                { "vlc", "Entertainment" },
                { "netflix", "Entertainment" },

                // Games
                { "steam", "Games" },
                { "epicgameslauncher", "Games" }
            };
        }

        private HashSet<string> InitializeProductiveApps()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "devenv", "code", "notepad", "notepad++", "sublime_text",
                "atom", "pycharm", "intellij", "eclipse", "netbeans",
                "winword", "excel", "powerpnt", "outlook", "teams",
                "slack", "zoom", "figma", "photoshop", "illustrator"
            };
        }

        private HashSet<string> InitializeDistractingApps()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "chrome", "firefox", "msedge", "safari", "opera",
                "spotify", "vlc", "netflix", "steam", "epicgameslauncher"
            };
        }
    }
}
