#nullable enable

using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.Core.Database
{
    /// <summary>
    /// EF Core database context representing the local SQLite database caching environment.
    /// </summary>
    public class AppDbContext : DbContext
    {
        private readonly CacheSchemaMigrator _schemaMigrator = new();

        public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
        
        public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
        
        public DbSet<GameModEntity> Mods => Set<GameModEntity>();

        public DbSet<SwgohGgRecommendationEntity> SwgohGgRecommendations => Set<SwgohGgRecommendationEntity>();

        public DbSet<SyncHistoryEntity> SyncHistory => Set<SyncHistoryEntity>();

        /// <summary>
        /// Gets the absolute path of the file-backed SQLite cache, when available.
        /// </summary>
        public string? CachePath
        {
            get
            {
                if (!Database.IsSqlite())
                {
                    return null;
                }

                var dataSource = Database.GetDbConnection().DataSource;
                if (string.IsNullOrWhiteSpace(dataSource) ||
                    string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return Path.GetFullPath(dataSource);
            }
        }

        /// <summary>
        /// Gets the result of the most recent cache schema migration pass.
        /// </summary>
        public CacheSchemaMigrationResult? LastSchemaMigration { get; private set; }

        /// <summary>
        /// Gets the directory where portable cache backups are written.
        /// </summary>
        public string? CacheBackupDirectory =>
            CachePath == null ? null : Path.Combine(Path.GetDirectoryName(CachePath)!, "backups");

        /// <summary>
        /// Creates the local cache schema on first launch.
        /// </summary>
        public void InitializeDatabase()
        {
            Database.EnsureCreated();

            if (Database.IsSqlite())
            {
                LastSchemaMigration = _schemaMigrator.Migrate(Database.GetDbConnection());
            }
        }

        /// <summary>
        /// Deletes and recreates the local cache schema without changing JSON settings.
        /// </summary>
        public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
        {
            await Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
            await Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

            if (Database.IsSqlite())
            {
                LastSchemaMigration = _schemaMigrator.Migrate(Database.GetDbConnection());
            }
        }

        /// <summary>
        /// Creates a portable snapshot of the file-backed SQLite cache.
        /// </summary>
        public async Task<string> BackupDatabaseAsync(CancellationToken cancellationToken = default)
        {
            if (!Database.IsSqlite())
            {
                throw new NotSupportedException("Cache backup is currently supported only for SQLite.");
            }

            var dataSource = Database.GetDbConnection().DataSource;
            if (string.IsNullOrWhiteSpace(dataSource) ||
                string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A file-backed SQLite cache is required for backup.");
            }

            // Backups must be self-describing even when a caller uses the context directly
            // instead of going through the application composition root.
            InitializeDatabase();

            var databasePath = Path.GetFullPath(dataSource);
            var databaseDirectory = Path.GetDirectoryName(databasePath);
            if (string.IsNullOrWhiteSpace(databaseDirectory))
            {
                throw new InvalidOperationException("The SQLite cache path could not be determined.");
            }

            var backupDirectory = Path.Combine(databaseDirectory, "backups");
            Directory.CreateDirectory(backupDirectory);
            var backupPath = Path.Combine(
                backupDirectory,
                $"cache-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.db");

            var connection = Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                var escapedPath = backupPath.Replace("'", "''", StringComparison.Ordinal);
                command.CommandText = $"VACUUM INTO '{escapedPath}';";
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }

            return backupPath;
        }

        /// <summary>
        /// Restores a verified backup from the cache's backup directory.
        /// </summary>
        public async Task RestoreDatabaseAsync(
            string backupPath,
            CancellationToken cancellationToken = default)
        {
            if (!Database.IsSqlite())
            {
                throw new NotSupportedException("Cache restore is currently supported only for SQLite.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var databasePath = CachePath;
            var backupDirectory = CacheBackupDirectory;
            if (databasePath == null || backupDirectory == null)
            {
                throw new InvalidOperationException("A file-backed SQLite cache is required for restore.");
            }

            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            {
                throw new FileNotFoundException("The selected cache backup was not found.", backupPath);
            }

            var fullBackupPath = Path.GetFullPath(backupPath);
            var backupRoot = backupDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullBackupPath.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cache restore is limited to files in the cache backup directory.");
            }

            ValidateBackup(fullBackupPath);
            var temporaryPath = databasePath + $".restore-{Guid.NewGuid():N}.tmp";
            var rollbackPath = databasePath + $".restore-rollback-{Guid.NewGuid():N}.tmp";
            var hadExistingDatabase = File.Exists(databasePath);

            try
            {
                Database.CloseConnection();
                if (hadExistingDatabase)
                {
                    File.Copy(databasePath, rollbackPath, overwrite: true);
                }

                File.Copy(fullBackupPath, temporaryPath, overwrite: true);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, databasePath, overwrite: true);
                ChangeTracker.Clear();
                InitializeDatabase();
                ValidateBackup(databasePath);
            }
            catch
            {
                Database.CloseConnection();
                if (File.Exists(rollbackPath))
                {
                    File.Move(rollbackPath, databasePath, overwrite: true);
                    ChangeTracker.Clear();
                }
                else if (!hadExistingDatabase && File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }

                throw;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                if (File.Exists(rollbackPath))
                {
                    File.Delete(rollbackPath);
                }
            }
        }

        private static void ValidateBackup(string backupPath)
        {
            using var connection = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
            connection.Open();

            using (var integrityCommand = connection.CreateCommand())
            {
                integrityCommand.CommandText = "PRAGMA integrity_check;";
                var result = integrityCommand.ExecuteScalar()?.ToString();
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The selected cache backup failed SQLite integrity validation.");
                }
            }

            using var schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' " +
                "AND name IN ('Players', 'Characters', 'Mods', 'SwgohGgRecommendations');";
            var tableCount = Convert.ToInt32(schemaCommand.ExecuteScalar());
            if (tableCount < 4)
            {
                throw new InvalidDataException("The selected file is not a complete SWGOH Command Bridge cache.");
            }

            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText =
                "SELECT Version FROM \"__CacheSchema\" WHERE Id = 1;";
            var versionValue = versionCommand.ExecuteScalar();
            if (versionValue == null || versionValue == DBNull.Value)
            {
                throw new InvalidDataException("The selected cache backup has no schema version marker.");
            }

            var version = Convert.ToInt32(versionValue);
            if (version > CacheSchemaMigrator.CurrentVersion)
            {
                throw new InvalidDataException(
                    $"The selected cache backup uses schema version {version}, which this application does not support.");
            }
        }

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
                var appDir = AppDataPaths.ApplicationDirectory;
                
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

            modelBuilder.Entity<GameModEntity>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.PlayerAllyCode });
                entity.HasOne(e => e.Player)
                      .WithMany(p => p.Mods)
                      .HasForeignKey(e => e.PlayerAllyCode)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SwgohGgRecommendationEntity>(entity =>
            {
                entity.HasKey(e => new { e.CharacterId, e.PlayerAllyCode });
            });

            modelBuilder.Entity<SyncHistoryEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }
}
