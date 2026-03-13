using System.IO;
using Microsoft.EntityFrameworkCore;
using TekTomato.Models;

namespace TekTomato.Data
{
    /// <summary>
    /// Database context for the TekTomato SQLite database.
    /// </summary>
    public class TekTomatoDbContext : DbContext
    {
        /// <summary>
        /// Gets or sets the Pomodoro Sessions table.
        /// </summary>
        public DbSet<PomodoroSession> Sessions => Set<PomodoroSession>();

        /// <summary>
        /// Gets or sets the Application Settings table.
        /// </summary>
        public DbSet<Setting> Settings => Set<Setting>();

        /// <summary>
        /// Configures the database connection and model on creation.
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TekTomato");
            
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            var dbPath = Path.Combine(basePath, "tektomato.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        /// <summary>
        /// Configures entity relationships and indexes.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Indexes for Sessions
            modelBuilder.Entity<PomodoroSession>()
                .HasIndex(s => s.StartedAtUtc)
                .HasDatabaseName("IX_Sessions_StartedAtUtc");

            modelBuilder.Entity<PomodoroSession>()
                .HasIndex(s => new { s.SessionType, s.CompletedNormally })
                .HasDatabaseName("IX_Sessions_SessionType_CompletedNormally");
        }
    }
}