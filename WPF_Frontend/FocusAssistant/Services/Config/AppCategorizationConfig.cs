using FocusAssistant.Services.Config.interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Config
{
    public class AppCategorizationConfig : IAppCategorizationConfig
    {
        public Dictionary<string, string[]> ProductiveApps { get; private set; }

        public Dictionary<string, string[]> DistractingApps { get; private set; }

        public string[] WorkKeywords { get; private set; }

        public AppCategorizationConfig()
        {
            LoadConfig();
        }


        private void LoadConfig() 
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "app_categories.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<AppCategorizationConfig>(json);
                    ProductiveApps = config.ProductiveApps;
                    DistractingApps = config.DistractingApps;
                    WorkKeywords = config.WorkKeywords;
                }
                else
                {
                    ProductiveApps = new Dictionary<string, string[]>
                    {
                        ["Development"] = new[] { "devenv", "code", "pycharm", "intellij", "eclipse", "atom", "sublime_text", "notepad++" },
                        ["Communication"] = new[] { "outlook", "teams", "slack", "discord", "zoom", "skype", "telegram" },
                        ["Office"] = new[] { "word", "excel", "powerpoint", "onenote", "notion" },
                        ["Design"] = new[] { "photoshop", "illustrator", "figma", "sketch", "canva" }
                    };

                    DistractingApps = new Dictionary<string, string[]>
                    {
                        ["Web Browser"] = new[] { "chrome", "firefox", "edge", "safari", "opera" },
                        ["Entertainment"] = new[] { "spotify", "vlc", "netflix", "youtube", "steam", "epicgameslauncher" },
                        ["System"] = new[] { "explorer", "taskmgr", "regedit", "cmd", "powershell" }
                    };

                    WorkKeywords = new[] { "work", "project" };
                }
            }
            catch(Exception e) { 
            
                
            }
            
        }
    }
}
