using FocusAssistant.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusAssistant.Core.Reports
{
    /// <summary>What a minute of the day was spent on.</summary>
    public enum TimelineState
    {
        /// <summary>Nothing recorded - the app was not running, or tracking was paused.</summary>
        Untracked,

        Productive,

        Distracting,

        /// <summary>Away from the machine.</summary>
        Idle,
    }

    /// <summary>One run of consecutive minutes in the same state.</summary>
    public sealed record TimelineSegment(TimelineState State, DateTime Start, int Minutes)
    {
        public DateTime End => Start.AddMinutes(Minutes);
    }

    /// <summary>
    /// A minute-by-minute picture of one day, collapsed into runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built for the strip on the Today screen: one column per minute, coloured by state.
    /// A day is 1,440 minutes, which is a reasonable number of rectangles to draw, but
    /// most of them are identical to their neighbour - so they are collapsed into runs
    /// here rather than in the view, and a typical day becomes a few dozen shapes.
    /// </para>
    /// <para>
    /// This exists because a shape carries the answer to "how did today go" faster than
    /// any number can. A percentage says the day was 62% productive; the strip shows that
    /// it was one solid morning and a shredded afternoon, which is the part worth acting
    /// on.
    /// </para>
    /// </remarks>
    public static class DayTimeline
    {
        public const int MinutesPerDay = 24 * 60;

        /// <summary>
        /// Builds the timeline for <paramref name="date"/> from recorded activity.
        /// </summary>
        /// <param name="usages">Activity for that day, in any order.</param>
        /// <param name="date">The day to render.</param>
        /// <param name="fromHour">First hour shown; the small hours are usually empty.</param>
        /// <param name="toHour">Last hour shown, exclusive.</param>
        public static IReadOnlyList<TimelineSegment> Build(
            IReadOnlyList<AppUsage> usages, DateTime date, int fromHour = 6, int toHour = 24)
        {
            var dayStart = date.Date;
            var minutes = new TimelineState[MinutesPerDay];

            if (usages is not null)
            {
                foreach (var usage in usages)
                {
                    if (usage.Duration <= TimeSpan.Zero)
                        continue;

                    var startMinute = (int)(usage.StartTime - dayStart).TotalMinutes;
                    var endMinute = (int)(usage.EndTime - dayStart).TotalMinutes;

                    // Anything shorter than a minute still deserves a mark, or a day of
                    // quick switches would render as empty.
                    if (endMinute == startMinute)
                        endMinute = startMinute + 1;

                    startMinute = Math.Clamp(startMinute, 0, MinutesPerDay - 1);
                    endMinute = Math.Clamp(endMinute, 0, MinutesPerDay);

                    var state = usage.IsProductive ? TimelineState.Productive : TimelineState.Distracting;

                    for (var m = startMinute; m < endMinute; m++)
                    {
                        // Distraction wins a contested minute. Overlaps happen at the seam
                        // between two usages, and under-reporting distraction is the more
                        // flattering error - so take the less flattering one.
                        if (minutes[m] == TimelineState.Untracked || state == TimelineState.Distracting)
                            minutes[m] = state;
                    }
                }
            }

            var first = Math.Clamp(fromHour * 60, 0, MinutesPerDay);
            var last = Math.Clamp(toHour * 60, first, MinutesPerDay);

            var segments = new List<TimelineSegment>();
            var runState = minutes[first];
            var runStart = first;

            for (var m = first + 1; m <= last; m++)
            {
                var state = m < last ? minutes[m] : (TimelineState)(-1);
                if (state == runState)
                    continue;

                segments.Add(new TimelineSegment(runState, dayStart.AddMinutes(runStart), m - runStart));
                runState = state;
                runStart = m;
            }

            return segments;
        }

        /// <summary>
        /// The busiest hours of the day, for the "when are you sharpest" readout.
        /// </summary>
        public static IReadOnlyList<HourlyFocus> ByHour(IReadOnlyList<AppUsage> usages, DateTime date)
        {
            var dayStart = date.Date;
            var productive = new double[24];
            var distracted = new double[24];

            foreach (var usage in usages ?? [])
            {
                var hour = usage.StartTime.Hour;
                if (hour is < 0 or > 23)
                    continue;

                if (usage.IsProductive)
                    productive[hour] += usage.Duration.TotalMinutes;
                else
                    distracted[hour] += usage.Duration.TotalMinutes;
            }

            return Enumerable.Range(0, 24)
                .Select(h => new HourlyFocus(
                    Hour: h,
                    ProductiveMinutes: productive[h],
                    DistractedMinutes: distracted[h]))
                .ToList();
        }
    }

    /// <summary>Productive and distracted minutes within one hour of the day.</summary>
    public sealed record HourlyFocus(int Hour, double ProductiveMinutes, double DistractedMinutes)
    {
        public double TotalMinutes => ProductiveMinutes + DistractedMinutes;

        public double ProductiveShare => TotalMinutes <= 0 ? 0 : ProductiveMinutes / TotalMinutes;

        public string Label => Hour switch
        {
            0 => "12a",
            < 12 => $"{Hour}a",
            12 => "12p",
            _ => $"{Hour - 12}p",
        };
    }
}
