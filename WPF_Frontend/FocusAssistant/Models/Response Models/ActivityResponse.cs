using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FocusAssistant.Models.Response_Models
{
    public class ActivityResponse : BaseResponse
    {
        [JsonPropertyName("action_taken")]
        public string ActionTaken { get; set; }

        [JsonPropertyName("distraction_risk")]
        public double DistractionRisk { get; set; }

        [JsonPropertyName("intervention_id")]
        public string InterventionId { get; set; }

        [JsonPropertyName("intervention_message")]
        public string InterventionMessage { get; set; }
    }
}
