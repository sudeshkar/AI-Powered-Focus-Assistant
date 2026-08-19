using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Models;
using FocusAssistant.Data.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Data.Stores
{
    /// <summary>
    /// The user's own corrections, cached in memory and backed by SQLite.
    /// </summary>
    /// <remarks>
    /// <see cref="Match"/> is called on the classification hot path - every window switch -
    /// so it can never touch the database directly. The whole table is loaded once at
    /// startup and kept in a concurrent dictionary; overrides are added by ones and the
    /// table stays small (nobody has thousands of applications), so an in-memory copy is
    /// cheap and a cold read from disk on the hot path would not be.
    /// </remarks>
    public sealed class SqliteUserOverrideStore : IUserOverrideStore
    {
        private readonly IDbContextFactory<FocusAssistantDbContext> _contextFactory;
        private readonly ILogger<SqliteUserOverrideStore> _logger;

        private readonly ConcurrentDictionary<string, bool> _overrides = new(StringComparer.OrdinalIgnoreCase);
        private volatile bool _loaded;

        public SqliteUserOverrideStore(
            IDbContextFactory<FocusAssistantDbContext> contextFactory,
            ILogger<SqliteUserOverrideStore> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Loads existing overrides from disk. Call once at startup, off the UI thread.</summary>
        public async Task WarmUpAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var rows = await context.UserOverrides.AsNoTracking().ToListAsync();

                foreach (var row in rows)
                    _overrides[row.AppName] = row.IsProductive;

                _loaded = true;
                _logger.LogInformation("Loaded {Count} user override(s)", rows.Count);
            }
            catch (Exception ex)
            {
                // An override store that failed to load is not a reason to stop
                // classifying - it just means no corrections apply yet this run.
                _logger.LogError(ex, "Could not load user overrides");
            }
        }

        public bool? Match(string? appName, string? windowTitle)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return null;

            return _overrides.TryGetValue(appName, out var isProductive) ? isProductive : null;
        }

        /// <summary>
        /// Records "this app is/isn't work", from the nudge window's "This is work" button
        /// or a manual correction elsewhere. Takes effect immediately for the in-memory
        /// copy that <see cref="Match"/> reads, and persists in the background.
        /// </summary>
        public async Task SetOverrideAsync(string appName, bool isProductive)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return;

            _overrides[appName] = isProductive;

            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();

                var existing = await context.UserOverrides
                    .FirstOrDefaultAsync(o => o.AppName == appName);

                if (existing is null)
                {
                    context.UserOverrides.Add(new UserOverride
                    {
                        AppName = appName,
                        IsProductive = isProductive,
                    });
                }
                else
                {
                    existing.IsProductive = isProductive;
                    existing.CreatedAt = DateTime.Now;
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // The in-memory override is already live, so classification is correct for
                // the rest of this run regardless; only persistence across restarts is at
                // risk here.
                _logger.LogError(ex, "Could not persist override for {App}", appName);
            }
        }

        public async Task RemoveOverrideAsync(string appName)
        {
            _overrides.TryRemove(appName, out _);

            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await context.UserOverrides
                    .Where(o => o.AppName == appName)
                    .ExecuteDeleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not remove override for {App}", appName);
            }
        }

        public bool IsLoaded => _loaded;
    }
}
