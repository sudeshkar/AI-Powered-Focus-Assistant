using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Application_Monitoring.Interfaces
{
    public interface IProductivityClassifier
    {
        bool IsProductiveActivity(string appName, string windowTitle);
        string CategorizeApp(string appName);
        double GetProductivityScore(string appName, string windowTitle);
    }
}
