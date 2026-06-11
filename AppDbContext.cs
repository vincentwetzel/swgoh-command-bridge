#nullable enable

using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Database
{
    /// <summary>
    /// EF Core database context representing the local SQLite database caching environment.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
        
        public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
        
        public DbSet<GameModEntity> Mods => Set<GameModEntity>();

        public DbSet<SwgohGgRecommendationEntity> SwgohGgRecommendations => Set<SwgohGgRecommendationEntity>();

        /// <summary>
        /// Parameterless constructor for design-time migrations and lightweight configuration.
        /// </summary>
        public AppDbContext()
        {
        }

        /// <summary>
        /// Constructor to accept customized database runtime configuration.
        /// </summary>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appDir = Path.Combine(appData, "swgoh-command-bridge");
                
                if (!Directory.Exists(appDir))
                {
                    Directory.CreateDirectory(appDir);
                }

                var dbPath = Path.Combine(appDir, "cache.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlayerEntity>(entity =>
            {
                entity.HasKey(e => e.AllyCode);
                entity.Property(e => e.Name).IsRequired();
            });

            modelBuilder.Entity<CharacterEntity>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.PlayerAllyCode });
                entity.HasOne(e => e.Player)
                      .WithMany(p => p.Characters)
                      .HasForeignKey(e => e.PlayerAllyCode)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SwgohGgRecommendationEntity>(entity =>
            {
                entity.HasKey(e => e.CharacterId);
            });
        }
    }
}