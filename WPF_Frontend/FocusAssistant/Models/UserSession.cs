using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class UserSession
    {
        [Key]
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime EndTime { get; set; }
        public int FocusTimeMinutes { get; set; }
        public int DistractionEvents { get; set; }
        public string MostUsedAppsJson { get; set; }="[]";
        [NotMapped]
        public List<string> MostUsedApps
        {
            get => string.IsNullOrEmpty(MostUsedAppsJson)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(MostUsedAppsJson);

            set => MostUsedAppsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }
        public double ProductivityScore { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public List<WorkSession> WorkSessions { get; set; } = new List<WorkSession>();
    }

}
