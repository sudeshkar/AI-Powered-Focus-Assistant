using FocusAssistant.Configuration;
using FocusAssistant.Data.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Deletes per-application activity older than the configured retention window.
    /// </summary>
    /// <remarks>
    /// Only <c>AppUsage</c> rows are deleted - the individual "you had Chrome open at
    /// 14:32" detail. <c>WorkSession</c> and <c>UserSession</c> rows, which hold the
    /// aggregated totals a day's history actually needs (productive time, top apps,
    /// streaks), are never touched by this and simply outlive their detail rows. That
    /// split is what "keep the trends, drop the specifics" means in practice: nothing
    /// extra had to be built to get it, because the aggregates were already computed and
    /// stored independently at the time each session ended.
    /// </remarks>
    public sealed class RetentionHostedService : IHostedService, IDisposable
    {
        /// <summary>
        /// Once at startup is enough - retention is measured in days, not minutes, so a
        /// service that only ever ran once a day at a fixed hour would be over-engineering
        /// for what this needs. Startup already happens roughly once a day for most people.
        /// </summary>
        private static readonly TimeSpan RecheckInterval = TimeSpan.FromHours(24);

        private readonly IDbContextFactory<FocusAssistantDbContext> _contextFactory;
        private readonly IOptionsMonitor<PrivacyOptions> _options;
        private readonly StartupState _startupState;
        private readonly ILogger<RetentionHostedService> _logger;

        private Timer? _timer;

        public RetentionHostedService(
            IDbContextFactory<FocusAssistantDbContext> contextFactory,
            IOptionsMonitor<PrivacyOptions> options,
            StartupState startupState,
            ILogger<RetentionHostedService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(_ => _ = RunAsync(), null, TimeSpan.Zero, RecheckInterval);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        private async Task RunAsync()
        {
            try
            {
                if (!await _startupState.DatabaseReady.ConfigureAwait(false))
                    return;

                var retentionDays = _options.CurrentValue.RetentionDays;
                if (retentionDays <= 0)
                    return;

                var cutoff = DateTime.Today.AddDays(-retentionDays);

                await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

                var deleted = await context.AppUsages
                    .Where(u => u.StartTime < cutoff)
                    .ExecuteDeleteAsync()
                    .ConfigureAwait(false);

                if (deleted > 0)
                    _logger.LogInformation(
                        "Retention: removed {Count} activity row(s) older than {Days} days",
                        deleted, retentionDays);
            }
            catch (Exception ex)
            {
                // Retention is housekeeping, not a feature the app depends on to run -
                // failing it silently for a run is far better than failing startup over it.
                _logger.LogError(ex, "Retention sweep failed");
            }
        }

        public void Dispose() => _timer?.Dispose();
    }
}
