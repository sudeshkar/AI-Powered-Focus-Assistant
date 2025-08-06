using FocusAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public class DataProcessor
    {
        private readonly LoggingService _loggingService;

        public DataProcessor(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public List<MLTrainingData> PrepareMLData(List<WorkSession> sessions)
        {
            var mlData = new List<MLTrainingData>();

            foreach (var session in sessions)
            {
                var appUsages = session.AppUsages.OrderBy(a => a.StartTime).ToList();

                for (int i = 0; i < appUsages.Count; i++)
                {
                    var usage = appUsages[i];
                    var mlRecord = new MLTrainingData
                    {
                        Timestamp = usage.StartTime,
                        TimeOfDay = usage.StartTime.Hour + (usage.StartTime.Minute / 60.0),
                        DayOfWeek = (int)usage.StartTime.DayOfWeek,
                        CurrentApp = usage.AppName,
                        WindowTitle = usage.WindowTitle,
                        SessionDurationMinutes = usage.Duration.TotalMinutes,
                        IsProductive = usage.IsProductive,
                        AppCategory = CategorizeApp(usage.AppName),
                        DistractionLevel = CalculateDistractionLevel(usage, session)
                    };

                    // Calculate contextual features
                    mlRecord.TimeSinceLastSwitch = i > 0 ?
                        (usage.StartTime - appUsages[i - 1].EndTime).TotalMinutes : 0;

                    mlRecord.AppSwitchesLast10Min = CountAppSwitches(appUsages, usage.StartTime, 10);
                    mlRecord.AppSwitchesLastHour = CountAppSwitches(appUsages, usage.StartTime, 60);
                    mlRecord.ProductivityScoreLast30Min = CalculateRecentProductivity(appUsages, usage.StartTime, 30);

                    // Add extended features
                    mlRecord.Features["session_productivity"] = session.ProductivityScore;
                    mlRecord.Features["total_session_switches"] = session.AppSwitches;
                    mlRecord.Features["session_length_hours"] = session.Duration.TotalHours;
                    mlRecord.Features["break_time_ratio"] = session.BreakTime.TotalMinutes / Math.Max(1, session.Duration.TotalMinutes);

                    mlData.Add(mlRecord);
                }
            }

            return mlData;
        }

        public DailyProductivityReport GenerateDailyReport(DateTime date)
        {
            var sessions = _loggingService.LoadWorkSessions(1)
                .Where(s => s.StartTime.Date == date.Date)
                .ToList();

            if (!sessions.Any())
                return new DailyProductivityReport { Date = date };

            var allUsages = sessions.SelectMany(s => s.AppUsages).ToList();

            var report = new DailyProductivityReport
            {
                Date = date,
                TotalWorkTimeHours = sessions.Sum(s => s.Duration.TotalHours),
                ProductiveTimeHours = sessions.Sum(s => s.ProductiveTime.TotalHours),
                DistractedTimeHours = sessions.Sum(s => s.DistractedTime.TotalHours),
                BreakTimeHours = sessions.Sum(s => s.BreakTime.TotalHours),
                ProductivityScore = sessions.Average(s => s.ProductivityScore),
                TotalAppSwitches = sessions.Sum(s => s.AppSwitches),
                AverageSessionLength = sessions.Average(s => s.Duration.TotalMinutes),
                NumberOfSessions = sessions.Count
            };

            // Top apps analysis
            var productiveApps = allUsages
                .Where(u => u.IsProductive)
                .GroupBy(u => u.AppName)
                .OrderByDescending(g => g.Sum(u => u.Duration.TotalMinutes))
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            var distractingApps = allUsages
                .Where(u => !u.IsProductive)
                .GroupBy(u => u.AppName)
                .OrderByDescending(g => g.Sum(u => u.Duration.TotalMinutes))
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            report.TopProductiveApps = productiveApps;
            report.TopDistractingApps = distractingApps;

            // Productivity by hour
            for (int hour = 0; hour < 24; hour++)
            {
                var hourlyUsages = allUsages.Where(u => u.StartTime.Hour == hour).ToList();
                if (hourlyUsages.Any())
                {
                    var hourlyProductivity = hourlyUsages.Where(u => u.IsProductive).Sum(u => u.Duration.TotalMinutes) /
                                           Math.Max(1, hourlyUsages.Sum(u => u.Duration.TotalMinutes)) * 100;
                    report.ProductivityByHour[hour] = hourlyProductivity;
                }
            }

            // Most/Least productive hours
            if (report.ProductivityByHour.Any())
            {
                var mostProductive = report.ProductivityByHour.OrderByDescending(kvp => kvp.Value).First();
                var leastProductive = report.ProductivityByHour.OrderBy(kvp => kvp.Value).First();

                report.MostProductiveHour = $"{mostProductive.Key:D2}:00 ({mostProductive.Value:F0}%)";
                report.LeastProductiveHour = $"{leastProductive.Key:D2}:00 ({leastProductive.Value:F0}%)";
            }

            return report;
        }

        private string CategorizeApp(string appName)
        {
            var categories = new Dictionary<string, string[]>
            {
                ["Development"] = new[] { "devenv", "code", "pycharm", "intellij", "eclipse", "atom", "sublime_text", "notepad++" },
                ["Communication"] = new[] { "outlook", "teams", "slack", "discord", "zoom", "skype", "telegram" },
                ["Web Browser"] = new[] { "chrome", "firefox", "edge", "safari", "opera" },
                ["Entertainment"] = new[] { "spotify", "vlc", "netflix", "youtube", "steam", "epicgameslauncher" },
                ["Office"] = new[] { "word", "excel", "powerpoint", "onenote", "notion" },
                ["Design"] = new[] { "photoshop", "illustrator", "figma", "sketch", "canva" },
                ["System"] = new[] { "explorer", "taskmgr", "regedit", "cmd", "powershell" }
            };

            foreach (var category in categories)
            {
                if (category.Value.Any(app => string.Equals(app, appName, StringComparison.OrdinalIgnoreCase)))
                    return category.Key;
            }

            return "Other";
        }

        private double CalculateDistractionLevel(AppUsage usage, WorkSession session)
        {
            // Simple distraction calculation (can be enhanced)
            double baseDistraction = usage.IsProductive ? 0.2 : 0.8;

            // Adjust based on session context
            double switchPenalty = Math.Min(0.3, session.AppSwitches / 100.0);
            double durationBonus = Math.Max(-0.2, -usage.Duration.TotalMinutes / 30.0 * 0.1);

            return Math.Max(0, Math.Min(1, baseDistraction + switchPenalty + durationBonus));
        }

        private int CountAppSwitches(List<AppUsage> usages, DateTime referenceTime, int minutesBack)
        {
            var cutoffTime = referenceTime.AddMinutes(-minutesBack);
            return usages.Count(u => u.StartTime >= cutoffTime && u.StartTime <= referenceTime);
        }

        private double CalculateRecentProductivity(List<AppUsage> usages, DateTime referenceTime, int minutesBack)
        {
            var cutoffTime = referenceTime.AddMinutes(-minutesBack);
            var recentUsages = usages.Where(u => u.StartTime >= cutoffTime && u.StartTime <= referenceTime).ToList();

            if (!recentUsages.Any()) return 0;

            var productiveTime = recentUsages.Where(u => u.IsProductive).Sum(u => u.Duration.TotalMinutes);
            var totalTime = recentUsages.Sum(u => u.Duration.TotalMinutes);

            return totalTime > 0 ? (productiveTime / totalTime) * 100 : 0;
        }
    }
}
