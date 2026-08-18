namespace FocusAssistant.Models
{
    /// <summary>
    /// One row in the Tracking view's live activity list.
    /// </summary>
    /// <remarks>
    /// A second class of this name used to be declared inside TrackingViewModel.cs;
    /// the view model bound to that one and this file was dead.
    /// </remarks>
    public class ActivityLogItem
    {
        public string AppName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string DurationText { get; set; } = string.Empty;
        public string TimeText { get; set; } = string.Empty;
        public bool IsProductive { get; set; }
    }
}
