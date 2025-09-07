using FocusAssistant.Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Services.ML
{
    public class MLDataProcessor : IMLDataProcessor
    {
        private readonly IProductivityClassifier _productivityClassifier;

        public MLDataProcessor(IProductivityClassifier productivityClassifier)
        {
            _productivityClassifier = productivityClassifier;
        }

        public async Task<List<MLTrainingData>> PrepareMLDataAsync(IEnumerable<WorkSession> sessions)
        {
            return await Task.Run(() =>
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
                            AppCategory = _productivityClassifier.CategorizeApp(usage.AppName),
                            DistractionLevel = CalculateDistractionLevel(usage, session)
                        };

                        // Calculate contextual features
                        mlRecord.TimeSinceLastSwitch = i > 0
                            ? (usage.StartTime - appUsages[i - 1].EndTime).TotalMinutes
                            : 0;

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
            });
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