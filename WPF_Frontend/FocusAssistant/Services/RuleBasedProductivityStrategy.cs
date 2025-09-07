using FocusAssistant.Services.Config.interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Service_Layer;

namespace FocusAssistant.Services
{
    public class RuleBasedProductivityStrategy : IProductivityStrategy
    {
        private readonly IAppCategorizationConfig _config;
        private readonly ILoggingService _loggingService;
        private readonly Dictionary<string, string> _categoryCache;
        private readonly Dictionary<string, bool> _productivityCache;

        public int Priority => 1;

        public RuleBasedProductivityStrategy(
            IAppCategorizationConfig config,
            ILoggingService loggingService)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _categoryCache = new Dictionary<string, string>();
            _productivityCache = new Dictionary<string, bool>();
        }


        public bool CanAnalyze(string appName, string windowTitle)
        {
            return !string.IsNullOrWhiteSpace(appName);
        }

        public string GetCategory(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return "Unknown";

            if (_categoryCache.TryGetValue(appName, out string cachedCategory))
                return cachedCategory;

            var category = DetermineCategory(appName);
            _categoryCache[appName] = category;

            return category;
        }

        public bool IsProductive(string appName, string windowTitle)
        {

            if (string.IsNullOrWhiteSpace(appName))
                return false;

            var cacheKey = $"{appName}:{windowTitle}";
            if (_productivityCache.TryGetValue(cacheKey, out bool cachedResult))
                return cachedResult;

            var result = AnalyzeProductivity(appName, windowTitle);
            _productivityCache[cacheKey] = result;

            return result;
        }

        public async Task<bool> IsProductiveAsync(string appName, string windowTitle)
        {
            return await Task.FromResult(IsProductive(appName, windowTitle));
        }

        private bool IsWorkRelatedContent(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return false;

            var lowerTitle = windowTitle.ToLowerInvariant();
            return _config.WorkKeywords.Any(keyword =>
                lowerTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }
        private bool AnalyzeProductivity(string appName, string windowTitle)
        {
            var normalizedAppName = appName.ToLowerInvariant();

            // Check if explicitly productive
            bool isProductiveApp = _config.ProductiveApps.Values
                .Any(apps => apps.Any(app =>
                    normalizedAppName.Contains(app, StringComparison.OrdinalIgnoreCase)));

            if (isProductiveApp)
            {
                _loggingService.LogDebug($"App '{appName}' categorized as productive (explicit match)");
                return true;
            }

            // Check if explicitly distracting
            bool isDistractingApp = _config.DistractingApps.Values
                .Any(apps => apps.Any(app =>
                    normalizedAppName.Contains(app, StringComparison.OrdinalIgnoreCase)));

            if (isDistractingApp)
            {
                // Check if window title suggests work-related content
                var isWorkRelated = IsWorkRelatedContent(windowTitle);
                _loggingService.LogDebug($"App '{appName}' is distracting, but work-related content: {isWorkRelated}");
                return isWorkRelated;
            }

            // Default to productive for unknown apps
            _loggingService.LogDebug($"App '{appName}' defaulted to productive (unknown app)");
            return true;
        }

        private string DetermineCategory(string appName)
        {
            var normalizedAppName = appName.ToLowerInvariant();

            // Check productive apps first
            foreach (var category in _config.ProductiveApps)
            {
                if (category.Value.Any(app =>
                    normalizedAppName.Contains(app, StringComparison.OrdinalIgnoreCase)))
                {
                    return category.Key;
                }
            }

            // Check distracting apps
            foreach (var category in _config.DistractingApps)
            {
                if (category.Value.Any(app =>
                    normalizedAppName.Contains(app, StringComparison.OrdinalIgnoreCase)))
                {
                    return category.Key;
                }
            }

            return "Other";
        }

        public double GetProductivityScore(string appName, string windowTitle)
        {
            throw new NotImplementedException();
        }
    }
}
