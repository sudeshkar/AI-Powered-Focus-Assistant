using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public class ProductivityAnalyzer : IProductivityAnalyzer
    {
        private readonly IProductivityStrategyFactory _strategyFactory;
        private readonly ProductivityStrategyType _selectedStrategy;


        public ProductivityAnalyzer(IProductivityStrategyFactory strategyFactory)
        {
            _strategyFactory = strategyFactory;

             
            _selectedStrategy = ProductivityStrategyType.RuleBased;
        }
        private IProductivityStrategy ActiveStrategy => _strategyFactory.GetStrategy(_selectedStrategy);

        public void AddStrategy(IProductivityStrategy strategy)
        {
            throw new NotImplementedException();
        }

        public string GetCategory(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName)) return "Unknown";
            return ActiveStrategy.GetCategory(appName);
        }

        public bool IsProductive(string appName, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(appName)) return false;
            return ActiveStrategy.IsProductive(appName, windowTitle);
        }

        public Task<bool> IsProductiveAsync(string appName, string windowTitle)
        {
            throw new NotImplementedException();
        }

        public void RemoveStrategy(IProductivityStrategy strategy)
        {
            throw new NotImplementedException();
        }

        public double Score(string appName, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(appName)) return 0;
            return ActiveStrategy.GetProductivityScore(appName, windowTitle);
        }
        
    }
}

