using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class RLInteraction
    {
        [Key]
        public string rId { get; set; } = Guid.NewGuid().ToString();
        public string wID { get; set; }

        [ForeignKey("wID")]
        public WorkSession WorkSession { get; set; }

        public string Action { get; set; }  
        public DateTime Timestamp { get; set; }
        public double Reward { get; set; }
        public string StateJson { get; set; }  
    }
}
