using FocusAssistant.Services.Config.interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Config
{
    public class FlaskConfiguration  
    {
        public string ApiUrl { get; set; } = "http://127.0.0.1:5000";
        public string ScriptPath { get; set; }
        public int StartupTimeoutSeconds { get; set; } = 10;
        public int HttpTimeoutSeconds { get; set; } = 30;

        public FlaskConfiguration()
        {
            // Start from the bin directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Go up three levels to reach project root
            string projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.FullName
                                 ?? throw new Exception("Project root not found");

            // Build paths
            string pythonBackendPath = Path.Combine(projectRoot, "Python_Backend");

            ScriptPath = Path.Combine(pythonBackendPath, "app.py");
            
        }

    }
}
