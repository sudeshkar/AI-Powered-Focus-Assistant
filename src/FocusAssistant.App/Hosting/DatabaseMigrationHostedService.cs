using FocusAssistant.Data.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Brings the database up to date without blocking startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="StartAsync"/> returns as soon as the work is queued. IHost.StartAsync
    /// awaits each hosted service in turn, so anything that blocks here delays every
    /// service after it — and the window is already on screen waiting on this one.
    /// </para>
    /// <para>
    /// The interesting case is a database created by the old <c>EnsureCreated()</c> call.
    /// Those files have the right tables and no __EFMigrationsHistory, so a plain
    /// Migrate() would try to CREATE TABLE over tables that already exist and throw.
    /// Such a database is instead <i>baselined</i>: the history table is created and the
    /// initial migration recorded as already applied, after which every later migration
    /// runs normally. The alternative — telling people to delete their history — is not
    /// something a product does.
    /// </para>
    /// </remarks>
    public sealed class DatabaseMigrationHostedService : IHostedService
    {
        /// <summary>
        /// The migration whose schema an EnsureCreated database already matches.
        /// Baselining records exactly this one as applied, and nothing else.
        /// </summary>
        private const string BaselineMigrationId = "20260819131740_InitialSchema";

        private readonly IDbContextFactory<FocusAssistantDbContext> _contextFactory;
        private readonly StartupState _startupState;
        private readonly ILogger<DatabaseMigrationHostedService> _logger;

        public DatabaseMigrationHostedService(
            IDbContextFactory<FocusAssistantDbContext> contextFactory,
            StartupState startupState,
            ILogger<DatabaseMigrationHostedService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _startupState = startupState ?? throw new ArgumentNullException(nameof(startupState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(() => MigrateAsync(cancellationToken), CancellationToken.None);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task MigrateAsync(CancellationToken cancellationToken)
        {
            try
            {
                // The data directory used to be created by a DI factory lambda, which meant
                // building the service provider touched the disk. Nothing in the object graph
                // should do I/O; it belongs here, on the path that actually needs it.
                Directory.CreateDirectory(AppPaths.DataDirectory);

                await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

                if (await NeedsBaseliningAsync(context, cancellationToken))
                    await BaselineAsync(context, cancellationToken);

                var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count > 0)
                {
                    _logger.LogInformation("Applying {Count} migration(s): {Migrations}",
                        pending.Count, string.Join(", ", pending));
                    await context.Database.MigrateAsync(cancellationToken);
                }

                _startupState.MarkDatabaseReady();
                _logger.LogInformation("Database ready at {Path}", AppPaths.DatabasePath);
            }
            catch (Exception ex)
            {
                // A failed migration is not a reason to kill the app — the user should see
                // what happened rather than a process that vanished.
                _logger.LogError(ex, "Database migration failed");
                _startupState.MarkDatabaseFailed(
                    $"The database could not be opened or upgraded: {ex.Message}");
            }
        }

        /// <summary>
        /// True for a database that has our tables but no migration history — i.e. one
        /// created by the EnsureCreated() call this service replaced.
        /// </summary>
        private static async Task<bool> NeedsBaseliningAsync(
            FocusAssistantDbContext context, CancellationToken cancellationToken)
        {
            if (!await context.Database.CanConnectAsync(cancellationToken))
                return false;

            var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (applied.Any())
                return false;

            // No history rows. Distinguish "brand new empty file" from "created by
            // EnsureCreated" by looking for a table the initial migration creates.
            var tables = await context.Database
                .SqlQueryRaw<string>(
                    "SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name = 'UserSessions'")
                .ToListAsync(cancellationToken);

            return tables.Count > 0;
        }

        private async Task BaselineAsync(FocusAssistantDbContext context, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Existing database has no migration history; baselining it at {Migration}",
                BaselineMigrationId);

            await context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
                "\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                "\"ProductVersion\" TEXT NOT NULL);",
                cancellationToken);

            // The indices are the one thing the initial migration adds that an EnsureCreated
            // database predates, so create them here rather than pretending the baseline was
            // a perfect match.
            string[] indices =
            [
                "CREATE INDEX IF NOT EXISTS \"IX_AppUsages_StartTime\" ON \"AppUsages\" (\"StartTime\");",
                "CREATE INDEX IF NOT EXISTS \"IX_AppUsages_wID\" ON \"AppUsages\" (\"wID\");",
                "CREATE INDEX IF NOT EXISTS \"IX_UserSessions_StartTime\" ON \"UserSessions\" (\"StartTime\");",
                "CREATE INDEX IF NOT EXISTS \"IX_WorkSessions_StartTime\" ON \"WorkSessions\" (\"StartTime\");",
                "CREATE INDEX IF NOT EXISTS \"IX_WorkSessions_SessionId\" ON \"WorkSessions\" (\"SessionId\");",
            ];

            foreach (var sql in indices)
                await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);

            var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.0";
            await context.Database.ExecuteSqlRawAsync(
                "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1});",
                [BaselineMigrationId, productVersion],
                cancellationToken);
        }
    }
}
