using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusAssistant.Core.Models
{
    /// <summary>How a work session ended, if it ended.</summary>
    public enum WorkSessionStatus
    {
        /// <summary>Started and not yet closed. On startup this means it was abandoned.</summary>
        Active,

        /// <summary>Closed normally, with totals computed from its own in-memory usages.</summary>
        Completed,

        /// <summary>Closed by the recovery pass, with totals recomputed from persisted rows.</summary>
        Recovered,
    }

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

        /// <summary>
        /// Whether this session was closed properly.
        /// </summary>
        /// <remarks>
        /// App usages are written as they complete rather than in one batch at the end, so
        /// a session killed by a crash or a power cut leaves its rows on disk but its own
        /// totals never computed. This column is how the recovery pass on the next start
        /// finds those sessions - EndTime alone cannot distinguish "still running" from
        /// "abandoned", and after a crash there is nothing still running.
        /// </remarks>
        public WorkSessionStatus Status { get; set; } = WorkSessionStatus.Active;
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
