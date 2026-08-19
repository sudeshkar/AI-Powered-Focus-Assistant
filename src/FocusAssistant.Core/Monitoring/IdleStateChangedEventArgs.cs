using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Monitoring
{
    public class IdleStateChangedEventArgs : EventArgs
    {
        public bool IsIdle { get; set; }
        public TimeSpan IdleTime { get; set; }
        public DateTime ChangeTime { get; set; }
    }
}
