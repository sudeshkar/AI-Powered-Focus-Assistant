using System.Collections.Generic;

namespace FocusAssistant.Core.Reports
{
    /// <summary>
    /// A day's focus summary, computed entirely from local data. Replaces the old
    /// AnalyticsResponse, which was deserialised from the Python backend and is
    /// gone along with it.
    /// </summary>
    public class DailyFocusReport
    {
        public string Date { get; set; } = string.Empty;
        public double ProductivityRate { get; set; }
        public int RecentInterventions { get; set; }

        /// <summary>Application name to activity count, for the day.</summary>
        public Dictionary<string, int> TopApps { get; set; } = new();

        public int TotalActivities { get; set; }
        public int ProductivityStreakDays { get; set; }
        public string Status { get; set; } = "success";
    }
}
