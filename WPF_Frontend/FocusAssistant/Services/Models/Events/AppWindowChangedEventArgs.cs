using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Models.Events
{
    public class AppWindowChangedEventArgs : EventArgs
    {
        public string PreviousAppName { get; set; }
        public string PreviousWindowTitle { get; set; }
        public string CurrentAppName { get; set; }
        public string CurrentWindowTitle { get; set; }
        public DateTime ChangeTime { get; set; }
    }
}
