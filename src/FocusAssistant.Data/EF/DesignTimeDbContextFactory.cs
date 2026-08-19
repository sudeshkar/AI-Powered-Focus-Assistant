using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;

namespace FocusAssistant.Data.EF
{
    /// <summary>
    /// Lets `dotnet ef` build a context without starting WPF.
    /// </summary>
    /// <remarks>
    /// Without this the tooling has to instantiate the application's host to find a
    /// context, which for a desktop app means spinning up a UI just to generate a
    /// migration. The connection string only has to be structurally right - the
    /// migration scaffolder never opens it.
    /// </remarks>
    public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FocusAssistantDbContext>
    {
        public FocusAssistantDbContext CreateDbContext(string[] args)
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusAssistant",
                "focusassistant.db");

            var options = new DbContextOptionsBuilder<FocusAssistantDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            return new FocusAssistantDbContext(options);
        }
    }
}
