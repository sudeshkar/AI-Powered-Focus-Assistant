using System;

namespace FocusAssistant.Services.Models.Events
{
    public class AppWindowChangedEventArgs : EventArgs
    {
        public string? PreviousAppName { get; set; }
        public string? PreviousWindowTitle { get; set; }
        public string CurrentAppName { get; set; } = string.Empty;
        public string? CurrentWindowTitle { get; set; }
        public DateTime ChangeTime { get; set; }
    }
}
