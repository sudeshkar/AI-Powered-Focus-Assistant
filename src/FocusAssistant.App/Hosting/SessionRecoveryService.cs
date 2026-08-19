using FocusAssistant.Core.Data.Abstractions;
using FocusAssistant.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Closes out sessions that a crash or a power cut left open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usage rows are written as they complete, but the totals on the session that owns
    /// them are computed once, at the end. A session killed mid-flight therefore leaves
    /// real activity on disk attached to a session row that still reads as zero minutes -
    /// the data is not lost, it is unaccounted for, which looks identical to the user.
    /// </para>
    /// <para>
    /// This runs once at startup and recomputes those totals from the rows that did
    /// survive. Any session still marked Active at startup was abandoned by definition:
    /// the process that owned it is gone, because this one just started.
    /// </para>
    /// </remarks>
    public sealed class SessionRecoveryService : IHostedService
    {
        private readonly IBaseService<WorkSession> _workSessions;
        private readonly IBaseService<UserSession> _userSessions;
        private readonly IBaseService<AppUsage> _appUsages;
        private readonly StartupState _startupState;
        private readonly ILogger<SessionRecoveryService> _logger;

        public SessionRecoveryService(
            IBaseService<WorkSession> workSessions,
            IBaseService<UserSession> userSessions,
            IBaseService<AppUsage> appUsages,
            StartupState startupState,
            ILogger<SessionRecoveryService> logger)
        {
            _workSessions = workSessions ?? throw new ArgumentNullException(nameof(workSessions));
            _userSessions = userSessions ?? throw new ArgumentNullException(nameof(userSessions));
            _appUsages = appUsages ?? throw new ArgumentNullException(nameof(appUsages));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(() => RecoverAsync(cancellationToken), CancellationToken.None);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task RecoverAsync(CancellationToken ct)
        {
            try
            {
                if (!await _startupState.DatabaseReady.ConfigureAwait(false))
                    return;

                var abandoned = await _workSessions
                    .QueryAsync(q => q.Where(s => s.Status == WorkSessionStatus.Active))
                    .ConfigureAwait(false);

                if (abandoned.Count == 0)
                    return;

                _logger.LogInformation("Recovering {Count} session(s) left open by a previous run",
                    abandoned.Count);

                foreach (var session in abandoned)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    await RecoverSessionAsync(session).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Recovery is best-effort housekeeping. Failing it must not stop the app
                // from starting and tracking today.
                _logger.LogError(ex, "Session recovery failed");
            }
        }

        private async Task RecoverSessionAsync(WorkSession session)
        {
            var usages = await _appUsages
                .QueryAsync(q => q.Where(u => u.wID == session.wID))
                .ConfigureAwait(false);

            session.AppUsages = usages;

            // The end time is unknowable - nothing recorded the moment the process died -
            // so the last thing actually observed is the honest answer. Inventing "now"
            // would credit the session with every hour the machine spent switched off.
            session.EndTime = usages.Count > 0
                ? usages.Max(u => u.EndTime)
                : session.StartTime;

            session.Duration = session.EndTime - session.StartTime;
            session.CalculateStatistics();
            session.Status = WorkSessionStatus.Recovered;

            await _workSessions.UpdateAsync(session).ConfigureAwait(false);

            var owner = (await _userSessions
                .QueryAsync(q => q.Where(u => u.SessionId == session.SessionId))
                .ConfigureAwait(false)).FirstOrDefault();

            if (owner is not null)
            {
                owner.EndTime = session.EndTime;
                owner.FocusTimeMinutes = (int)session.ProductiveTime.TotalMinutes;
                owner.ProductivityScore = session.ProductivityScore;
                owner.MostUsedApps = session.TopApps;
                await _userSessions.UpdateAsync(owner).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Recovered session {SessionId}: {Count} usages, {Minutes:F0} minutes productive",
                session.SessionId, usages.Count, session.ProductiveTime.TotalMinutes);
        }
    }
}
