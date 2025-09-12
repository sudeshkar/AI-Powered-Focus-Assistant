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

        public FocusAssistantDbContext(DbContextOptions<FocusAssistantDbContext> options)
        : base(options)
        {
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FocusAssistant",
                    "focusassistant.db"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
                optionsBuilder.UseSqlite($"Data Source={dbPath}")
                    .EnableSensitiveDataLogging() // For debugging
                    .EnableDetailedErrors();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // === Primary Keys ===
            modelBuilder.Entity<UserSession>()
                .HasKey(s => s.SessionId);

            modelBuilder.Entity<WorkSession>()
                .HasKey(w => w.wID);

            modelBuilder.Entity<AppUsage>()
                .HasKey(a => a.aID);

            modelBuilder.Entity<RLInteraction>()
                .HasKey(r => r.rId);

            // === Relationships ===

            // UserSession → WorkSessions (1:N)
            modelBuilder.Entity<UserSession>()
                .HasMany(u => u.WorkSessions)
                .WithOne(w => w.UserSession)
                .HasForeignKey(w => w.SessionId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // WorkSession → AppUsages (1:N)
            modelBuilder.Entity<WorkSession>()
                .HasMany(w => w.AppUsages)
                .WithOne(a => a.WorkSession)
                .HasForeignKey(a => a.wID)
                .OnDelete(DeleteBehavior.Cascade);

            // WorkSession → RLInteractions (1:N)
            modelBuilder.Entity<WorkSession>()
                .HasMany(w => w.RLInteractions)
                .WithOne(r => r.WorkSession)
                .HasForeignKey(r => r.wID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppUsage>()
            .Property(u => u.aID)
            .ValueGeneratedOnAdd();

            base.OnModelCreating(modelBuilder);
        }
    }
}
