using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FocusAssistant.Models.Response_Models
{
    /// <summary>GET /analytics — today's activity totals from the backend.</summary>
    public class AnalyticsResponse : BaseResponse
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("productivity_rate")]
        public double ProductivityRate { get; set; }

        [JsonPropertyName("recent_interventions")]
        public int RecentInterventions { get; set; }

        /// <summary>
        /// Application name to activity count. The backend has always sent an
        /// object here; typing this as List&lt;string&gt; made every response fail
        /// to deserialise.
        /// </summary>
        [JsonPropertyName("top_apps")]
        public Dictionary<string, int> TopApps { get; set; } = new();

        [JsonPropertyName("total_activities")]
        public int TotalActivities { get; set; }

        /// <summary>Locally computed; not part of the backend payload.</summary>
        [JsonIgnore]
        public int ProductivityStreaks { get; set; }
    }
}
