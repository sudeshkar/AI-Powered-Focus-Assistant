using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusAssistant.Models
{
    /// <summary>One run of the application, spanning its work sessions.</summary>
    public class UserSession
    {
        [Key]
        public string SessionId { get; set; } = Guid.NewGuid().ToString();

        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime EndTime { get; set; }
        public int FocusTimeMinutes { get; set; }
        public int DistractionEvents { get; set; }
        public double ProductivityScore { get; set; }

        /// <summary>SQLite has no list type, so this is stored as a JSON array.</summary>
        public string MostUsedAppsJson { get; set; } = "[]";

        [NotMapped]
        public List<string> MostUsedApps
        {
            // Never returns null: malformed JSON yields an empty list rather than
            // propagating a null the callers would have to guard.
            get
            {
                if (string.IsNullOrWhiteSpace(MostUsedAppsJson))
                    return new List<string>();

                try
                {
                    return JsonSerializer.Deserialize<List<string>>(MostUsedAppsJson) ?? new List<string>();
                }
                catch (JsonException)
                {
                    return new List<string>();
                }
            }
            set => MostUsedAppsJson = JsonSerializer.Serialize(value ?? new List<string>());
        }

        [JsonIgnore]
        public List<WorkSession> WorkSessions { get; set; } = new();
    }
}
