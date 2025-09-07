using System;
using System.Collections.Generic;

namespace FocusAssistant.Models
{
    public class DailyReport
    {
        public DateTime Date { get; set; }
        public TimeSpan ProductiveTime { get; set; }
        public TimeSpan DistractedTime { get; set; }
        public int ProductivityStreak { get; set; }
        public Dictionary<string, double> TopApps { get; set; } = new Dictionary<string, double>();
    }
}