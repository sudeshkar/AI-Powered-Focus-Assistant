using FocusAssistant.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// The single definition of how focused a stretch of time was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There were three. WorkSession.CalculateStatistics divided productive time by
    /// productive plus distracted; SessionEngine.GetTodayStatistics and AnalyticsServiceSQL
    /// divided by total duration, which includes breaks; DailyReportGenerator divided focus
    /// minutes by wall-clock elapsed. The same day showed three different percentages on
    /// three different screens, and none of them was wrong so much as unowned.
    /// </para>
    /// <para>
    /// This is the owner. Every screen asks it, so the number moves together everywhere.
    /// </para>
    /// <para>
    /// It is not simply a percentage of productive time, because that misses how focus
    /// actually fails. An hour split into twenty pieces is not the same as an unbroken
    /// hour, even when the productive minutes are identical - so fragmentation costs
    /// points and a long unbroken stretch earns them back. Breaks are excluded entirely
    /// rather than counted against you: stepping away is not a lapse in focus, and an app
    /// that scores you down for lunch is one you stop believing.
    /// </para>
    /// </remarks>
    public static class FocusScorer
    {
        /// <summary>
        /// A stretch of at least this long counts as real deep work, and is what the
        /// streak bonus is measured against.
        /// </summary>
        private static readonly TimeSpan DeepWorkStretch = TimeSpan.FromMinutes(25);

        /// <summary>
        /// Switches per hour beyond this start costing points. Below it, switching is just
        /// how work is done - reading docs while writing code is not distraction.
        /// </summary>
        private const double TolerableSwitchesPerHour = 30;

        /// <summary>Most that fragmentation can take off, so a bad patch is never unrecoverable.</summary>
        private const double MaxFragmentationPenalty = 20;

        /// <summary>Most that an unbroken stretch can add.</summary>
        private const double MaxStreakBonus = 10;

        /// <summary>
        /// Scores a day from its recorded activity.
        /// </summary>
        public static FocusScore Score(IReadOnlyList<AppUsage> usages, TimeSpan breakTime)
        {
            if (usages is null || usages.Count == 0)
                return FocusScore.Empty;

            var productive = TimeSpan.FromTicks(usages.Where(u => u.IsProductive).Sum(u => u.Duration.Ticks));
            var distracted = TimeSpan.FromTicks(usages.Where(u => !u.IsProductive).Sum(u => u.Duration.Ticks));
            var engaged = productive + distracted;

            if (engaged <= TimeSpan.Zero)
                return FocusScore.Empty;

            var longestStretch = LongestProductiveStretch(usages);

            // Breaks are deliberately not in the denominator: time away is not time lost.
            var baseScore = productive.TotalMinutes / engaged.TotalMinutes * 100;

            var hours = Math.Max(engaged.TotalHours, 0.25);
            var switchesPerHour = usages.Count / hours;
            var fragmentation = switchesPerHour <= TolerableSwitchesPerHour
                ? 0
                : Math.Min(MaxFragmentationPenalty,
                    (switchesPerHour - TolerableSwitchesPerHour) / TolerableSwitchesPerHour * MaxFragmentationPenalty);

            var streakBonus = Math.Min(MaxStreakBonus,
                longestStretch.TotalMinutes / DeepWorkStretch.TotalMinutes * MaxStreakBonus);

            var score = Math.Clamp(baseScore - fragmentation + streakBonus, 0, 100);

            return new FocusScore(
                Value: (int)Math.Round(score),
                ProductiveTime: productive,
                DistractedTime: distracted,
                BreakTime: breakTime,
                LongestStretch: longestStretch,
                AppSwitches: usages.Count,
                FragmentationPenalty: fragmentation,
                StreakBonus: streakBonus);
        }

        /// <summary>
        /// The longest run of back-to-back productive activity.
        /// </summary>
        /// <remarks>
        /// Consecutive productive usages are treated as one stretch when they abut, because
        /// switching from the editor to the terminal and back has not broken anyone's
        /// concentration. A gap longer than the tolerance means something else happened in
        /// between - an idle period, or activity that was not recorded - and the stretch
        /// ends there.
        /// </remarks>
        public static TimeSpan LongestProductiveStretch(IReadOnlyList<AppUsage> usages)
        {
            if (usages is null || usages.Count == 0)
                return TimeSpan.Zero;

            var gapTolerance = TimeSpan.FromMinutes(2);
            var ordered = usages.OrderBy(u => u.StartTime).ToList();

            var longest = TimeSpan.Zero;
            var current = TimeSpan.Zero;
            DateTime? previousEnd = null;

            foreach (var usage in ordered)
            {
                if (!usage.IsProductive)
                {
                    current = TimeSpan.Zero;
                    previousEnd = null;
                    continue;
                }

                var contiguous = previousEnd is null || usage.StartTime - previousEnd.Value <= gapTolerance;
                current = contiguous ? current + usage.Duration : usage.Duration;

                if (current > longest)
                    longest = current;

                previousEnd = usage.EndTime;
            }

            return longest;
        }
    }

    /// <summary>
    /// A focus score and the parts it was built from.
    /// </summary>
    /// <remarks>
    /// The components travel with the number so the UI can explain it. A bare "68" invites
    /// the question "why", and an app that cannot answer that is one people stop trusting.
    /// </remarks>
    public readonly record struct FocusScore(
        int Value,
        TimeSpan ProductiveTime,
        TimeSpan DistractedTime,
        TimeSpan BreakTime,
        TimeSpan LongestStretch,
        int AppSwitches,
        double FragmentationPenalty,
        double StreakBonus)
    {
        public static FocusScore Empty => new(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
            TimeSpan.Zero, 0, 0, 0);

        public bool HasData => ProductiveTime + DistractedTime > TimeSpan.Zero;

        /// <summary>A short, non-judgemental label for the score.</summary>
        public string Band => Value switch
        {
            >= 80 => "Deep focus",
            >= 60 => "Solid",
            >= 40 => "Mixed",
            _ when HasData => "Scattered",
            _ => "No data yet",
        };
    }
}
