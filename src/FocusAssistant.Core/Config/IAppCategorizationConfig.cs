using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Config
{
    public interface IAppCategorizationConfig
    {
        Dictionary<string, string[]> ProductiveApps { get; }
        Dictionary<string, string[]> DistractingApps { get; }
        string[] WorkKeywords { get; }
    }
}
