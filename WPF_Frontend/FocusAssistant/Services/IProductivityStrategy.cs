using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public interface IProductivityStrategy
    {
        bool IsProductive(string appName, string windowTitle);
        Task<bool> IsProductiveAsync(string appName, string windowTitle);
        int Priority { get; }
        bool CanAnalyze(string appName, string windowTitle);
        string GetCategory(string appName);
        double GetProductivityScore(string appName, string windowTitle);
    }
}
