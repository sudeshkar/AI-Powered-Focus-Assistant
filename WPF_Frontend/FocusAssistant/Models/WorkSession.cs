using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class WorkSession
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan ProductiveTime { get; set; }
        public TimeSpan DistractedTime { get; set; }
        public TimeSpan BreakTime { get; set; }
        public double ProductivityScore { get; set; }
        public int AppSwitches { get; set; }
        public List<AppUsage> AppUsages { get; set; }
        public List<string> TopApps { get; set; }

        public WorkSession()
        {
            AppUsages = new List<AppUsage>();
            TopApps = new List<string>();
        }

        public void CalculateStatistics()
        {
            if (!AppUsages.Any()) return;

            // Calculate productive vs distracted time
            ProductiveTime = TimeSpan.FromTicks(
                AppUsages.Where(a => a.IsProductive).Sum(a => a.Duration.Ticks));

            DistractedTime = TimeSpan.FromTicks(
                AppUsages.Where(a => !a.IsProductive).Sum(a => a.Duration.Ticks));

            // Calculate app switches
            AppSwitches = AppUsages.Count;

            // Calculate productivity score (0-100)
            var totalActiveTime = ProductiveTime + DistractedTime;
            if (totalActiveTime.TotalMinutes > 0)
            {
                ProductivityScore = (ProductiveTime.TotalMinutes / totalActiveTime.TotalMinutes) * 100;
            }

            // Get top 5 most used apps
            TopApps = AppUsages
                .GroupBy(a => a.AppName)
                .OrderByDescending(g => g.Sum(a => a.Duration.TotalMinutes))
                .Take(5)
                .Select(g => g.Key)
                .ToList();
        }
    }

    public class SessionStatistics
    {
        public int TotalSessions { get; set; }
        public TimeSpan TotalWorkTime { get; set; }
        public TimeSpan TotalProductiveTime { get; set; }
        public TimeSpan TotalBreakTime { get; set; }
        public TimeSpan AverageSessionLength { get; set; }
        public double ProductivityScore { get; set; }
        public int TotalAppSwitches { get; set; }
    }
}
