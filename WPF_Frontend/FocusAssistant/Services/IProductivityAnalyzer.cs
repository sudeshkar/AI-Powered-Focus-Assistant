using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public interface IProductivityAnalyzer
    {
        bool IsProductive(string appName, string windowTitle);
        Task<bool> IsProductiveAsync(string appName, string windowTitle);
        string GetCategory(string appName);
        double Score(string appName, string windowTitle);
        void AddStrategy(IProductivityStrategy strategy);
        void RemoveStrategy(IProductivityStrategy strategy);
    }
}
