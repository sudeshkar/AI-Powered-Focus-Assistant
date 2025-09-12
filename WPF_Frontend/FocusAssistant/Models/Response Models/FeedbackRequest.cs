using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FocusAssistant.Models.Response_Models
{
    // POST /feedback request body
    public class FeedbackRequest
    {
        [JsonPropertyName("intervention_id")]
        public string InterventionId { get; set; }

        [JsonPropertyName("helpful")]
        public bool Helpful { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonPropertyName("productivity_change")]
        public int ProductivityChange { get; set; }

    }
}
