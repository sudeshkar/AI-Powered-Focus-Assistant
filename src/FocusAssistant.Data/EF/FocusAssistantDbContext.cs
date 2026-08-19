using Microsoft.EntityFrameworkCore;
using FocusAssistant.Core.Models;
using System;
using System.IO;

namespace FocusAssistant.Data.EF
{
    public class FocusAssistantDbContext : DbContext
    {
        public DbSet<UserSession> UserSessions { get; set; } = null!;
        public DbSet<WorkSession> WorkSessions { get; set; } = null!;
        public DbSet<AppUsage> AppUsages { get; set; } = null!;

        public FocusAssistantDbContext(DbContextOptions<FocusAssistantDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Fallback for design-time tooling. At runtime the connection is supplied
        /// by the DbContext factory registered in App.xaml.cs.
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusAssistant",
                "focusassistant.db");

            // EnableSensitiveDataLogging is deliberately not set: it writes parameter
            // values, and those parameters include window titles.
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserSession>()
                .HasKey(s => s.SessionId);

            modelBuilder.Entity<WorkSession>()
                .HasKey(w => w.wID);

            modelBuilder.Entity<AppUsage>()
                .HasKey(a => a.aID);

            // UserSession -> WorkSessions (1:N)
            modelBuilder.Entity<UserSession>()
                .HasMany(u => u.WorkSessions)
                .WithOne(w => w.UserSession)
                .HasForeignKey(w => w.SessionId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // WorkSession -> AppUsages (1:N)
            modelBuilder.Entity<WorkSession>()
                .HasMany(w => w.AppUsages)
                .WithOne(a => a.WorkSession)
                .HasForeignKey(a => a.wID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppUsage>()
                .Property(u => u.aID)
                .ValueGeneratedOnAdd();

            // Every analytics query filters on a start time or joins on wID, and
            // without these SQLite scans the whole table - which grows by a row per
            // application switch, all day, forever.
            modelBuilder.Entity<AppUsage>().HasIndex(a => a.StartTime);
            modelBuilder.Entity<AppUsage>().HasIndex(a => a.wID);
            modelBuilder.Entity<WorkSession>().HasIndex(w => w.StartTime);
            modelBuilder.Entity<UserSession>().HasIndex(u => u.StartTime);

            base.OnModelCreating(modelBuilder);
        }
    }
}
