using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FocusAssistant.Models.Response_Models
{
    // GET /analytics response
    public class AnalyticsResponse : BaseResponse
    {
        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("productivity_rate")]
        public double ProductivityRate { get; set; }

        [JsonPropertyName("recent_interventions")]
        public int RecentInterventions { get; set; }

        [JsonPropertyName("top_apps")]
        public List<string> TopApps { get; set; }

        [JsonPropertyName("total_activities")]
        public int TotalActivities { get; set; }

        public int productivityStreaks { get; set; }
    }
}
