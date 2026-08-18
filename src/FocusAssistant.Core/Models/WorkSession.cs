using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusAssistant.Core.Models
{
    /// <summary>A single tracked stretch of work, holding the app usages within it.</summary>
    public class WorkSession
    {
        [Key]
        public string wID { get; set; } = Guid.NewGuid().ToString();

        public string SessionId { get; set; } = string.Empty;

        [ForeignKey(nameof(SessionId))]
        [JsonIgnore]
        public UserSession? UserSession { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan ProductiveTime { get; set; }
        public TimeSpan DistractedTime { get; set; }
        public TimeSpan BreakTime { get; set; }
        public double ProductivityScore { get; set; }
        public int AppSwitches { get; set; }

        public List<AppUsage> AppUsages { get; set; } = new();

        // The old RLInteractions navigation (paired with a now-deleted RLInteraction
        // entity) tracked calls to the Python RL backend, which no longer exists.
        // Phase 2 adds InterventionOutcome as its on-device replacement.

        /// <summary>SQLite has no list type, so this is stored as a JSON array.</summary>
        public string TopAppsJson { get; set; } = "[]";

        [NotMapped]
        public List<string> TopApps
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TopAppsJson))
                    return new List<string>();

                try
                {
                    return JsonSerializer.Deserialize<List<string>>(TopAppsJson) ?? new List<string>();
                }
                catch (JsonException)
                {
                    return new List<string>();
                }
            }
            set => TopAppsJson = JsonSerializer.Serialize(value ?? new List<string>());
        }

        /// <summary>Recomputes the derived totals from the collected app usages.</summary>
        public void CalculateStatistics()
        {
            if (AppUsages.Count == 0)
                return;

            ProductiveTime = TimeSpan.FromTicks(AppUsages.Where(a => a.IsProductive).Sum(a => a.Duration.Ticks));
            DistractedTime = TimeSpan.FromTicks(AppUsages.Where(a => !a.IsProductive).Sum(a => a.Duration.Ticks));
            AppSwitches = AppUsages.Count;

            var activeTime = ProductiveTime + DistractedTime;
            ProductivityScore = activeTime.TotalMinutes > 0
                ? ProductiveTime.TotalMinutes / activeTime.TotalMinutes * 100
                : 0;

            TopApps = AppUsages
                .GroupBy(a => a.AppName)
                .OrderByDescending(g => g.Sum(a => a.Duration.TotalMinutes))
                .Take(5)
                .Select(g => g.Key)
                .ToList();
        }
    }

    /// <summary>Aggregated totals across a set of work sessions.</summary>
    public class SessionStatistics
    {
        public int TotalSessions { get; set; }
        public TimeSpan TotalWorkTime { get; set; }
        public TimeSpan TotalProductiveTime { get; set; }
        public TimeSpan TotalDistractedTime { get; set; }
        public TimeSpan TotalBreakTime { get; set; }
        public TimeSpan AverageSessionLength { get; set; }
        public double ProductivityScore { get; set; }
        public int TotalAppSwitches { get; set; }
    }
}
