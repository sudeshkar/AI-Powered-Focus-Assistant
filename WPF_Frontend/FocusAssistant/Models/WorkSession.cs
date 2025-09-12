using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class WorkSession
    {
        [Key]
        public string wID { get; set; } = Guid.NewGuid().ToString();

        // Foreign key to UserSession
        public string SessionId { get; set; }

        [ForeignKey("SessionId")]

        [System.Text.Json.Serialization.JsonIgnore]
        public UserSession UserSession { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan ProductiveTime { get; set; }
        public TimeSpan DistractedTime { get; set; }
        public TimeSpan BreakTime { get; set; }
        public double ProductivityScore { get; set; }
        public int AppSwitches { get; set; }

        // Related data
        public List<AppUsage> AppUsages { get; set; } = new List<AppUsage>();
        public List<RLInteraction> RLInteractions { get; set; } = new List<RLInteraction>();

        // Store TopApps as JSON
        public string? TopAppsJson { get; set; } = "[]";

        [NotMapped]
        public List<string> TopApps
        {
            get => string.IsNullOrEmpty(TopAppsJson)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(TopAppsJson);

            set => TopAppsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }

        public void CalculateStatistics()
        {
            if (!AppUsages.Any()) return;

            ProductiveTime = TimeSpan.FromTicks(
                AppUsages.Where(a => a.IsProductive).Sum(a => a.Duration.Ticks));

            DistractedTime = TimeSpan.FromTicks(
                AppUsages.Where(a => !a.IsProductive).Sum(a => a.Duration.Ticks));

            AppSwitches = AppUsages.Count;

            var totalActiveTime = ProductiveTime + DistractedTime;
            if (totalActiveTime.TotalMinutes > 0)
            {
                ProductivityScore = (ProductiveTime.TotalMinutes / totalActiveTime.TotalMinutes) * 100;
            }

            TopApps = AppUsages
                .GroupBy(a => a.AppName)
                .OrderByDescending(g => g.Sum(a => a.Duration.TotalMinutes))
                .Take(5)
                .Select(g => g.Key)
                .ToList();
        }
    }


    public class SessionStatistics
    {
        public int TotalSessions { get; set; }
        public TimeSpan TotalWorkTime { get; set; }
        public TimeSpan TotalProductiveTime { get; set; }
        public TimeSpan TotalBreakTime { get; set; }
        public TimeSpan AverageSessionLength { get; set; }
        public TimeSpan TotalDistractedTime { get; set; }
        public double ProductivityScore { get; set; }
        public int TotalAppSwitches { get; set; }
    }
}
