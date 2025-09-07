using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class ReadinessResponse
    {
        public bool IsReady { get; set; }
        public int ActivitiesNeeded { get; set; }
    }
}
