using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class AppUsage
    {
        [Key]
        public int aID { get; set; }

        public string wID { get; set; }

        [ForeignKey("WID")]
        public WorkSession WorkSession { get; set; }
        public string AppName { get; set; }
        public string WindowTitle { get; set; }
        public DateTime StartTime { get; set; }= DateTime.Now;
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsProductive { get; set; }

        public AppUsage()
        {
            StartTime = DateTime.Now;
        }
    }


}
