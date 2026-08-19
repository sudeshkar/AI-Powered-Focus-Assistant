using FocusAssistant.Data.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FocusAssistant.Privacy
{
    /// <summary>
    /// The "delete my data" button's backing service.
    /// </summary>
    /// <remarks>
    /// Deletes activity history - <c>AppUsage</c>, <c>WorkSession</c>, <c>UserSession</c>,
    /// and <c>InterventionOutcome</c> rows - and then <c>VACUUM</c>s so the file actually
    /// shrinks; SQLite does not reclaim space from deleted rows on its own, and a delete
    /// button that leaves the file the same size looks like it failed. <c>UserOverride</c>
    /// rows are deliberately kept: those are corrections the user made on purpose, closer
    /// to a setting than to tracked activity, and silently discarding them on a data-wipe
    /// would make every "This is work" click need to be redone.
    /// </remarks>
    public sealed class DataManagementService
    {
        private readonly IDbContextFactory<FocusAssistantDbContext> _contextFactory;
        private readonly ILogger<DataManagementService> _logger;

        public DataManagementService(
            IDbContextFactory<FocusAssistantDbContext> contextFactory,
            ILogger<DataManagementService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task DeleteAllActivityAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Children before parents: AppUsage/InterventionOutcome reference WorkSession,
            // and WorkSession references UserSession. Cascade delete would handle this too,
            // but being explicit here means the order is obvious from reading it rather than
            // depending on a foreign-key configuration living in a different file.
            await context.AppUsages.ExecuteDeleteAsync();
            await context.InterventionOutcomes.ExecuteDeleteAsync();
            await context.WorkSessions.ExecuteDeleteAsync();
            await context.UserSessions.ExecuteDeleteAsync();

            await context.Database.ExecuteSqlRawAsync("VACUUM;");

            _logger.LogInformation("All tracked activity deleted at the user's request");
        }

        /// <summary>Opens the folder holding the database, logs, and downloaded model in Explorer.</summary>
        public void OpenDataFolder()
        {
            try
            {
                if (Directory.Exists(Hosting.AppPaths.DataDirectory))
                    System.Diagnostics.Process.Start("explorer.exe", Hosting.AppPaths.DataDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not open the data folder");
            }
        }
    }
}
