using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class RLInteraction
    {
        [Key]
        public int rId { get; set; }
        public string SessionId { get; set; }
        public string Action { get; set; }  
        public DateTime Timestamp { get; set; }
        public double Reward { get; set; }
        public string StateJson { get; set; }  
    }
}
