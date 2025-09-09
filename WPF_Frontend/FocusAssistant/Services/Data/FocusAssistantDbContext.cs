using Microsoft.EntityFrameworkCore;
using FocusAssistant.Models;
using System;
using System.IO;

namespace FocusAssistant.Data
{
    public class FocusAssistantDbContext : DbContext
    {
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<WorkSession> WorkSessions { get; set; }
        public DbSet<AppUsage> AppUsages { get; set; }
        public DbSet<RLInteraction> RLInteractions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusAssistant",
                "focusassistant.db"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // UserSession → WorkSessions
            modelBuilder.Entity<UserSession>()
                .HasMany(u => u.WorkSessions)
                .WithOne(w => w.UserSession)
                .HasForeignKey(w => w.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppUsage>()
            .Property(u => u.aID)
            .ValueGeneratedOnAdd();

            // WorkSession → AppUsages
            modelBuilder.Entity<WorkSession>()
                .HasMany(w => w.AppUsages)
                .WithOne(a => a.WorkSession)
                .HasForeignKey(a => a.wID)
                .OnDelete(DeleteBehavior.Cascade);

            // WorkSession → RLInteractions
            modelBuilder.Entity<WorkSession>()
                .HasMany(w => w.RLInteractions)
                .WithOne()
                .HasForeignKey(r => r.rId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}
