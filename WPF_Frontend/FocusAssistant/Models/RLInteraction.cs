using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FocusAssistant.Models
{
    /// <summary>One intervention offered by the agent and how it scored.</summary>
    public class RLInteraction
    {
        [Key]
        public string rId { get; set; } = Guid.NewGuid().ToString();

        public string wID { get; set; } = string.Empty;

        [ForeignKey(nameof(wID))]
        public WorkSession? WorkSession { get; set; }

        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Reward { get; set; }
        public string StateJson { get; set; } = "{}";
    }
}
