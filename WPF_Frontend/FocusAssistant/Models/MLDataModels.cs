using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class MLTrainingData
    {
        public string UserId { get; set; } = "user_default";
        public DateTime Timestamp { get; set; }
        public double TimeOfDay { get; set; } // Hour of day (0-23.99)
        public int DayOfWeek { get; set; } // 0=Sunday, 6=Saturday
        public string CurrentApp { get; set; }
        public string WindowTitle { get; set; }
        public double SessionDurationMinutes { get; set; }
        public double TimeSinceLastSwitch { get; set; } // Minutes
        public double IdleTimeBefore { get; set; } // Minutes of idle before this activity
        public int AppSwitchesLast10Min { get; set; }
        public int AppSwitchesLastHour { get; set; }
        public double ProductivityScoreLast30Min { get; set; }
        public bool IsProductive { get; set; } // Target variable for ML
        public double DistractionLevel { get; set; } // 0.0 to 1.0
        public string AppCategory { get; set; } // "Development", "Communication", "Entertainment", etc.
        public Dictionary<string, object> Features { get; set; }

        public MLTrainingData()
        {
            Features = new Dictionary<string, object>();
            Timestamp = DateTime.Now;
            TimeOfDay = DateTime.Now.Hour + (DateTime.Now.Minute / 60.0);
            DayOfWeek = (int)DateTime.Now.DayOfWeek;
        }
    }

    // Aggregated daily statistics for trend analysis
    public class DailyProductivityReport
    {
        public DateTime Date { get; set; }
        public double TotalWorkTimeHours { get; set; }
        public double ProductiveTimeHours { get; set; }
        public double DistractedTimeHours { get; set; }
        public double BreakTimeHours { get; set; }
        public double ProductivityScore { get; set; }
        public int TotalAppSwitches { get; set; }
        public double AverageSessionLength { get; set; }
        public int NumberOfSessions { get; set; }
        public List<string> TopProductiveApps { get; set; }
        public List<string> TopDistractingApps { get; set; }
        public Dictionary<int, double> ProductivityByHour { get; set; } // Hour -> Productivity Score
        public string MostProductiveHour { get; set; }
        public string LeastProductiveHour { get; set; }

        public DailyProductivityReport()
        {
            TopProductiveApps = new List<string>();
            TopDistractingApps = new List<string>();
            ProductivityByHour = new Dictionary<int, double>();
        }
    }

    // Real-time intervention data for RL model
    public class InterventionData
    {
        public string InterventionId { get; set; }
        public DateTime Timestamp { get; set; }
        public string TriggerReason { get; set; } // "high_distraction", "long_session", "app_switch_spike"
        public string InterventionType { get; set; } // "break_reminder", "focus_tip", "block_suggestion"
        public string Message { get; set; }
        public bool UserAccepted { get; set; }
        public double EffectivenessScore { get; set; } // 0-1, based on subsequent behavior
        public Dictionary<string, object> Context { get; set; }

        public InterventionData()
        {
            InterventionId = Guid.NewGuid().ToString();
            Context = new Dictionary<string, object>();
        }
    }
}
