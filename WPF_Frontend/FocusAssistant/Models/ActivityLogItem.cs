using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class ActivityLogItem
    {
        public string AppName { get; set; }
        public string WindowTitle { get; set; }
        public TimeSpan Duration { get; set; }
        public string DurationText { get; set; }
        public string TimeText { get; set; }
        public bool IsProductive { get; set; }
        public string ProductivityIcon { get; set; }
    }
}
