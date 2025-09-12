using FocusAssistant.Models;
using FocusAssistant.Services.Config.interfaces;
using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public class MLBasedProductivityStrategy : IProductivityStrategy
    {
        private readonly IAppCategorizationConfig _config;
        private readonly IActivityService _mlService;

        public int Priority => 10;

        public MLBasedProductivityStrategy(IAppCategorizationConfig config, IActivityService mlService)
        {
            _config = config;
            _mlService = mlService; 
        }
        public string GetCategory(string appName)
        {
            foreach (var category in _config.ProductiveApps)
                if (category.Value.Contains(appName.ToLower())) return category.Key;
            foreach (var category in _config.DistractingApps)
                if (category.Value.Contains(appName.ToLower())) return category.Key;
            return "Other";
        }
         
        private bool IsProductiveRuleBased(string appName, string windowTitle)
        {
            bool isProductiveApp = _config.ProductiveApps.Values.Any(apps =>
                apps.Any(app => appName.ToLower().Contains(app.ToLower())));

            bool isDistractingApp = _config.DistractingApps.Values.Any(apps =>
                apps.Any(app => appName.ToLower().Contains(app.ToLower())));

            if (isProductiveApp) return true;
            if (isDistractingApp) return IsWorkRelatedContent(windowTitle);

            return true; 
        }

        public bool IsProductive(string appName, string windowTitle)
        {
            bool isProductiveApp = _config.ProductiveApps.Values.Any(apps => apps.Contains(appName.ToLower()));
            bool isDistractingApp = _config.DistractingApps.Values.Any(apps => apps.Contains(appName.ToLower()));
            if (isProductiveApp) return true;
            if (isDistractingApp) return IsWorkRelatedContent(windowTitle);

            return true;
        }
        private bool IsWorkRelatedContent(string windowTitle)
        {
            var lowerTitle = windowTitle.ToLower();
            return _config.WorkKeywords.Any(keyword => lowerTitle.Contains(keyword));
        }

        public bool CanAnalyze(string appName, string windowTitle)
        {
            throw new NotImplementedException();
        }

        public double GetProductivityScore(string appName, string windowTitle)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsProductiveAsync(string appName, string windowTitle)
        {
            throw new NotImplementedException();
        }
    }
}
