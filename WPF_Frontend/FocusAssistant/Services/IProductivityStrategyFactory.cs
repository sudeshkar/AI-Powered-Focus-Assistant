using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public interface IProductivityStrategyFactory
    {
        IProductivityStrategy GetStrategy(ProductivityStrategyType strategyType);

    }
    public enum ProductivityStrategyType
    {
        RuleBased,
        MachineLearning
    }
}
