using FocusAssistant.Core.Models;
using FocusAssistant.Data.EF;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Data.Queries
{
    /// <summary>
    /// Reads a day's raw activity, which the scoring and timeline code then interprets.
    /// </summary>
    /// <remarks>
    /// Deliberately returns rows rather than summaries. The screens need several different
    /// views of the same day - a score, a timeline, an hourly breakdown, a top-apps list -
    /// and computing each with its own query meant four round trips and, historically, four
    /// subtly different definitions of the same number. One read, several interpretations.
    /// </remarks>
    public sealed class DayQueryService
    {
        private readonly IDbContextFactory<FocusAssistantDbContext> _contextFactory;

        public DayQueryService(IDbContextFactory<FocusAssistantDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        /// <summary>All recorded activity for one day.</summary>
        public async Task<List<AppUsage>> GetUsagesAsync(DateTime date, CancellationToken ct = default)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Indexed on StartTime, and read-only, so no change tracking.
            return await context.AppUsages
                .AsNoTracking()
                .Where(u => u.StartTime >= start && u.StartTime < end)
                .OrderBy(u => u.StartTime)
                .ToListAsync(ct);
        }

        /// <summary>Activity across a range of days, for the weekly and monthly views.</summary>
        public async Task<List<AppUsage>> GetUsagesAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var start = from.Date;
            var end = to.Date.AddDays(1);

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            return await context.AppUsages
                .AsNoTracking()
                .Where(u => u.StartTime >= start && u.StartTime < end)
                .OrderBy(u => u.StartTime)
                .ToListAsync(ct);
        }

        /// <summary>Total break time recorded for a day.</summary>
        public async Task<TimeSpan> GetBreakTimeAsync(DateTime date, CancellationToken ct = default)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var sessions = await context.WorkSessions
                .AsNoTracking()
                .Where(s => s.StartTime >= start && s.StartTime < end)
                .Select(s => s.BreakTime)
                .ToListAsync(ct);

            return TimeSpan.FromTicks(sessions.Sum(b => b.Ticks));
        }

        /// <summary>
        /// Consecutive days ending today on which anything was recorded.
        /// </summary>
        /// <remarks>
        /// One query over distinct dates, not one query per day. The previous streak
        /// calculation issued up to 365 sequential queries, each opening its own context,
        /// on the path the dashboard awaited before it could paint.
        /// </remarks>
        public async Task<int> GetStreakDaysAsync(DateTime today, CancellationToken ct = default)
        {
            var since = today.Date.AddDays(-365);

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var days = await context.AppUsages
                .AsNoTracking()
                .Where(u => u.StartTime >= since)
                .Select(u => u.StartTime.Date)
                .Distinct()
                .ToListAsync(ct);

            var tracked = days.ToHashSet();

            var streak = 0;
            for (var day = today.Date; tracked.Contains(day); day = day.AddDays(-1))
                streak++;

            return streak;
        }
    }
}
