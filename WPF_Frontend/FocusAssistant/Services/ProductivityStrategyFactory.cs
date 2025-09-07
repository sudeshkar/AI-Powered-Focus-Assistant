using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public class ProductivityStrategyFactory : IProductivityStrategyFactory
    {
        private readonly RuleBasedProductivityStrategy _ruleBasedStrategy;
        private readonly MLBasedProductivityStrategy _mlBasedStrategy;

        public ProductivityStrategyFactory(
            RuleBasedProductivityStrategy ruleBasedStrategy,
            MLBasedProductivityStrategy mlBasedStrategy)
        {
            _ruleBasedStrategy = ruleBasedStrategy;
            _mlBasedStrategy = mlBasedStrategy;
        }
        public IProductivityStrategy GetStrategy(ProductivityStrategyType strategyType)
        {
            return strategyType switch
            {
                ProductivityStrategyType.RuleBased => _ruleBasedStrategy,
                ProductivityStrategyType.MachineLearning => _mlBasedStrategy,
                _ => throw new ArgumentException($"Unknown strategy type: {strategyType}")
            };
        }
    }
}
