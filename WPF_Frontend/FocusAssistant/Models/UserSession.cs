using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class UserSession
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int FocusTimeMinutes { get; set; }
        public int DistractionEvents { get; set; }
        public List<string> MostUsedApps { get; set; }
        public double ProductivityScore { get; set; }

        public UserSession()
        {
            SessionId = Guid.NewGuid().ToString();
            StartTime = DateTime.Now;
            MostUsedApps = new List<string>();
        }
    }

}
