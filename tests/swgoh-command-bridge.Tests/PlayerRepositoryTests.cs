#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Database.Repositories;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class PlayerRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public PlayerRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task SavePlayerAsync_PreservesExistingCharacterPriorityAcrossRefresh()
    {
        var repository = new PlayerRepository(
            _context,
            NullLogger<PlayerRepository>.Instance);

        await repository.SavePlayerAsync(CreatePlayer("Original", 80));
        await repository.SavePlayerAsync(CreatePlayer("Refreshed", 0));

        var cached = await repository.GetPlayerAsync("123456789");

        Assert.NotNull(cached);
        Assert.Equal("Refreshed", cached!.Name);
        Assert.Equal(80, Assert.Single(cached.Characters).Priority);
    }

    [Fact]
    public async Task SavePlayerAsync_WhenReplacementFails_RollsBackExistingAccountRows()
    {
        var repository = new PlayerRepository(
            _context,
            NullLogger<PlayerRepository>.Instance);

        await repository.SavePlayerAsync(CreatePlayer("Original", 80));
        using (var command = _connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TRIGGER FailReplacementMod BEFORE INSERT ON Mods " +
                "WHEN NEW.Id = 'FAIL' BEGIN SELECT RAISE(ABORT, 'synthetic replacement failure'); END;";
            command.ExecuteNonQuery();
        }

        var replacement = CreatePlayer("Replacement", 0);
        replacement.Mods.Single().Id = "FAIL";

        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.SavePlayerAsync(replacement));

        var cached = await repository.GetPlayerAsync("123456789");
        Assert.NotNull(cached);
        Assert.Equal("Original", cached!.Name);
        Assert.Equal(80, Assert.Single(cached.Characters).Priority);
        Assert.Equal("MOD", Assert.Single(cached.Mods).Id);
    }

    [Fact]
    public async Task DeletePlayerAsync_RemovesPlayerAndAccountOwnedRows()
    {
        var repository = new PlayerRepository(
            _context,
            NullLogger<PlayerRepository>.Instance);

        await repository.SavePlayerAsync(CreatePlayer("To Remove", 60));
        _context.SyncHistory.Add(new SyncHistoryEntity
        {
            AllyCode = "123456789",
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow,
            Status = "completed"
        });
        await _context.SaveChangesAsync();

        var removed = await repository.DeletePlayerAsync("123456789");

        Assert.True(removed);
        Assert.Empty(await _context.Players.ToListAsync());
        Assert.Empty(await _context.Characters.ToListAsync());
        Assert.Empty(await _context.Mods.ToListAsync());
        Assert.Empty(await _context.SyncHistory.ToListAsync());
        Assert.False(await repository.DeletePlayerAsync("123456789"));
    }

    [Fact]
    public async Task ResetDatabaseAsync_RecreatesAnEmptyCache()
    {
        _context.Players.Add(new PlayerEntity
        {
            AllyCode = "123456789",
            Name = "Cached Player"
        });
        await _context.SaveChangesAsync();

        await _context.ResetDatabaseAsync();

        Assert.Empty(await _context.Players.ToListAsync());
    }

    [Fact]
    public void InitializeDatabase_RecordsSupportedCacheSchemaVersion()
    {
        _context.InitializeDatabase();

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT \"Version\" FROM \"__CacheSchema\" WHERE \"Id\" = 1;";

        Assert.Equal((long)CacheSchemaMigrator.CurrentVersion, command.ExecuteScalar());
    }

    [Fact]
    public void InitializeDatabase_ReportsAppliedMigrationsAndIsIdempotent()
    {
        _context.InitializeDatabase();

        Assert.NotNull(_context.LastSchemaMigration);
        Assert.Equal(0, _context.LastSchemaMigration!.PreviousVersion);
        Assert.Equal(CacheSchemaMigrator.CurrentVersion, _context.LastSchemaMigration.CurrentVersion);
        Assert.True(_context.LastSchemaMigration.Changed);
        Assert.Contains("2: mod stat snapshots", _context.LastSchemaMigration.AppliedMigrations);
        Assert.Contains("3: recommendation provenance", _context.LastSchemaMigration.AppliedMigrations);
        Assert.Contains("4: player sync timestamps", _context.LastSchemaMigration.AppliedMigrations);
        Assert.Contains("5: sync outcome history", _context.LastSchemaMigration.AppliedMigrations);
        Assert.Contains("6: account-scoped recommendations", _context.LastSchemaMigration.AppliedMigrations);
        Assert.Contains("7: character portrait catalog mappings", _context.LastSchemaMigration.AppliedMigrations);
        Assert.Contains("8: mod primary correction", _context.LastSchemaMigration.AppliedMigrations);
        Assert.Contains("9: tier-list priority layout", _context.LastSchemaMigration.AppliedMigrations);

        _context.InitializeDatabase();

        Assert.NotNull(_context.LastSchemaMigration);
        Assert.Equal(CacheSchemaMigrator.CurrentVersion, _context.LastSchemaMigration!.PreviousVersion);
        Assert.False(_context.LastSchemaMigration.Changed);
    }

    [Fact]
    public void CacheSchemaMigrator_RebuildsMissingTablesForAnOlderPartialCache()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE \"__CacheSchema\" (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"Version\" INTEGER NOT NULL);" +
                "INSERT INTO \"__CacheSchema\" (\"Id\", \"Version\") VALUES (1, 1);" +
                "CREATE TABLE \"Players\" (\"AllyCode\" TEXT NOT NULL PRIMARY KEY, \"Name\" TEXT NOT NULL, \"Level\" INTEGER NOT NULL, \"GalacticPower\" INTEGER NOT NULL);";
            command.ExecuteNonQuery();
        }

        var result = new CacheSchemaMigrator().Migrate(connection);

        Assert.Equal(1, result.PreviousVersion);
        Assert.Equal(CacheSchemaMigrator.CurrentVersion, result.CurrentVersion);
        Assert.Contains("3: recommendation provenance", result.AppliedMigrations);
        Assert.Contains("4: player sync timestamps", result.AppliedMigrations);
        Assert.Contains("5: sync outcome history", result.AppliedMigrations);
        Assert.Contains("6: account-scoped recommendations", result.AppliedMigrations);
        Assert.Contains("7: character portrait catalog mappings", result.AppliedMigrations);
        Assert.Contains("8: mod primary correction", result.AppliedMigrations);
        Assert.Contains("9: tier-list priority layout", result.AppliedMigrations);

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' " +
            "AND name IN ('Players', 'Characters', 'Mods', 'SwgohGgRecommendations', 'SyncHistory');";
        Assert.Equal(5L, tableCommand.ExecuteScalar());
    }

    [Fact]
    public void CacheSchemaMigrator_ScopesLegacyRecommendationsToEmptyAllyCode()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE \"__CacheSchema\" (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"Version\" INTEGER NOT NULL);" +
                "INSERT INTO \"__CacheSchema\" (\"Id\", \"Version\") VALUES (1, 5);" +
                "CREATE TABLE \"SwgohGgRecommendations\" (" +
                "\"CharacterId\" TEXT NOT NULL PRIMARY KEY, \"Source\" TEXT NOT NULL, " +
                "\"RecommendationSchemaVersion\" INTEGER NOT NULL, \"SourceUrl\" TEXT NOT NULL, " +
                "\"PrimaryStatsJson\" TEXT NOT NULL, \"SetRecommendationsJson\" TEXT NOT NULL, " +
                "\"PopularityPercentage\" REAL NOT NULL, \"LastUpdatedUtc\" TEXT NOT NULL);" +
                "INSERT INTO \"SwgohGgRecommendations\" VALUES " +
                "('CHARACTER', 'legacy', 1, 'fixture', '{}', '[]', 0, '2026-01-01T00:00:00Z');";
            command.ExecuteNonQuery();
        }

        var result = new CacheSchemaMigrator().Migrate(connection);

        Assert.Equal(CacheSchemaMigrator.CurrentVersion, result.CurrentVersion);
        using var scopeCommand = connection.CreateCommand();
        scopeCommand.CommandText =
            "SELECT \"PlayerAllyCode\" FROM \"SwgohGgRecommendations\" WHERE \"CharacterId\" = 'CHARACTER';";
        Assert.Equal(string.Empty, scopeCommand.ExecuteScalar());
    }

    [Fact]
    public void CacheSchemaMigrator_AddsCurrentColumnsToLegacyTables()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE \"__CacheSchema\" (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"Version\" INTEGER NOT NULL);" +
                "INSERT INTO \"__CacheSchema\" (\"Id\", \"Version\") VALUES (1, 1);" +
                "CREATE TABLE \"Mods\" (\"Id\" TEXT NOT NULL, \"PlayerAllyCode\" TEXT NOT NULL, \"Slot\" INTEGER NOT NULL);";
            command.ExecuteNonQuery();
        }

        new CacheSchemaMigrator().Migrate(connection);

        using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = "PRAGMA table_info('Mods');";
        using var reader = columnCommand.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.Contains("PrimaryStatType", columns);
        Assert.Contains("PrimaryStatValue", columns);
        Assert.Contains("SecondaryStatsJson", columns);

        using var characterColumnCommand = connection.CreateCommand();
        characterColumnCommand.CommandText = "PRAGMA table_info('Characters');";
        using var characterReader = characterColumnCommand.ExecuteReader();
        var characterColumns = new List<string>();
        while (characterReader.Read())
        {
            characterColumns.Add(characterReader.GetString(1));
        }

        Assert.Contains("PriorityTier", characterColumns);
        Assert.Contains("PriorityOrder", characterColumns);

        using var playerColumnCommand = connection.CreateCommand();
        playerColumnCommand.CommandText = "PRAGMA table_info('Players');";
        using var playerReader = playerColumnCommand.ExecuteReader();
        var playerColumns = new List<string>();
        while (playerReader.Read())
        {
            playerColumns.Add(playerReader.GetString(1));
        }

        Assert.Contains("LastSyncedUtc", playerColumns);
    }

    [Fact]
    public void CacheSchemaMigrator_CorrectsFixedModPrimaryStatsFromVersionSevenCache()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var migrator = new CacheSchemaMigrator();
        migrator.Migrate(connection);

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "UPDATE \"__CacheSchema\" SET \"Version\" = 7 WHERE \"Id\" = 1;" +
                "INSERT INTO \"Mods\" (\"Id\", \"PlayerAllyCode\", \"CharacterId\", \"Set\", \"Slot\", \"Level\", \"Tier\", \"Rarity\", \"PrimaryStatType\", \"PrimaryStatValue\", \"SecondaryStatsJson\") VALUES " +
                "('square', '123456789', 'CHARACTER', 1, 1, 15, 5, 5, 'Accuracy', 0.05, '[]')," +
                "('diamond', '123456789', 'CHARACTER', 4, 3, 15, 5, 5, 'CriticalAvoidance', 0.10, '[]');";
            command.ExecuteNonQuery();
        }

        var result = migrator.Migrate(connection);

        Assert.Contains("8: mod primary correction", result.AppliedMigrations);
        using var readCommand = connection.CreateCommand();
        readCommand.CommandText = "SELECT \"PrimaryStatType\" FROM \"Mods\" ORDER BY \"Slot\";";
        using var reader = readCommand.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("OffensePercent", reader.GetString(0));
        Assert.True(reader.Read());
        Assert.Equal("DefensePercent", reader.GetString(0));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1, 8)]
    [InlineData(2, 7)]
    [InlineData(3, 6)]
    [InlineData(4, 5)]
    [InlineData(5, 4)]
    [InlineData(6, 3)]
    [InlineData(7, 2)]
    [InlineData(8, 1)]
    public void CacheSchemaMigrator_UpgradesEverySupportedVersion(
        int previousVersion,
        int expectedMigrationCount)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE \"__CacheSchema\" (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"Version\" INTEGER NOT NULL);" +
                $"INSERT INTO \"__CacheSchema\" (\"Id\", \"Version\") VALUES (1, {previousVersion});";
            command.ExecuteNonQuery();
        }

        var result = new CacheSchemaMigrator().Migrate(connection);

        Assert.Equal(previousVersion, result.PreviousVersion);
        Assert.Equal(CacheSchemaMigrator.CurrentVersion, result.CurrentVersion);
        Assert.Equal(expectedMigrationCount, result.AppliedMigrations.Count);
        Assert.Equal(CacheSchemaMigrator.CurrentVersion, ReadSchemaVersion(connection));
    }

    [Fact]
    public void CacheSchemaMigrator_RollsBackAnIncompleteRecommendationMigration()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE \"__CacheSchema\" (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"Version\" INTEGER NOT NULL);" +
                "INSERT INTO \"__CacheSchema\" (\"Id\", \"Version\") VALUES (1, 5);" +
                "CREATE TABLE \"SwgohGgRecommendations\" (\"CharacterId\" TEXT NOT NULL PRIMARY KEY);";
            command.ExecuteNonQuery();
        }

        Assert.Throws<SqliteException>(() => new CacheSchemaMigrator().Migrate(connection));

        Assert.Equal(5, ReadSchemaVersion(connection));
        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' " +
            "AND name = 'SwgohGgRecommendations_v6';";
        Assert.Equal(0L, tableCommand.ExecuteScalar());
    }

    [Fact]
    public void InitializeDatabase_RejectsAForwardSchemaVersion()
    {
        _context.InitializeDatabase();
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "UPDATE \"__CacheSchema\" SET \"Version\" = 99 WHERE \"Id\" = 1;";
            command.ExecuteNonQuery();
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => _context.InitializeDatabase());

        Assert.Contains("newer than this application supports", exception.Message);
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Version\" FROM \"__CacheSchema\" WHERE \"Id\" = 1;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    [Fact]
    public async Task BackupDatabaseAsync_RejectsInMemoryCache()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _context.BackupDatabaseAsync());

        Assert.Contains("file-backed SQLite cache", exception.Message);
    }

    [Fact]
    public async Task BackupDatabaseAsync_CreatesTimestampedCopyBesideCache()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "swgoh-command-bridge-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);

        try
        {
            var databasePath = Path.Combine(testDirectory, "cache.db");
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var backupPath = await context.BackupDatabaseAsync();

            Assert.True(File.Exists(backupPath));
            Assert.StartsWith(
                Path.Combine(testDirectory, "backups") + Path.DirectorySeparatorChar,
                backupPath,
                StringComparison.OrdinalIgnoreCase);

            using var backupConnection = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
            await backupConnection.OpenAsync();
            using var schemaCommand = backupConnection.CreateCommand();
            schemaCommand.CommandText = "SELECT Version FROM \"__CacheSchema\" WHERE Id = 1;";
            Assert.Equal(CacheSchemaMigrator.CurrentVersion, Convert.ToInt32(await schemaCommand.ExecuteScalarAsync()));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreDatabaseAsync_RestoresVerifiedBackup()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "swgoh-command-bridge-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);

        try
        {
            var databasePath = Path.Combine(testDirectory, "cache.db");
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.InitializeDatabase();
            context.Players.Add(new PlayerEntity
            {
                AllyCode = "123456789",
                Name = "Original Player"
            });
            await context.SaveChangesAsync();

            var backupPath = await context.BackupDatabaseAsync();
            context.Players.RemoveRange(await context.Players.ToListAsync());
            await context.SaveChangesAsync();

            await context.RestoreDatabaseAsync(backupPath);

            Assert.Equal("Original Player", (await context.Players.SingleAsync()).Name);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreDatabaseAsync_RejectsBackupFromAnUnsupportedFutureSchema()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "swgoh-command-bridge-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);

        try
        {
            var databasePath = Path.Combine(testDirectory, "cache.db");
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.InitializeDatabase();
            context.Players.Add(new PlayerEntity
            {
                AllyCode = "123456789",
                Name = "Original Player"
            });
            await context.SaveChangesAsync();

            var backupPath = await context.BackupDatabaseAsync();
            using (var backupConnection = new SqliteConnection($"Data Source={backupPath}"))
            {
                await backupConnection.OpenAsync();
                using var command = backupConnection.CreateCommand();
                command.CommandText = "UPDATE \"__CacheSchema\" SET \"Version\" = 99 WHERE \"Id\" = 1;";
                await command.ExecuteNonQueryAsync();
            }

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => context.RestoreDatabaseAsync(backupPath));

            Assert.Contains("schema version 99", exception.Message);
            Assert.Equal("Original Player", (await context.Players.SingleAsync()).Name);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreDatabaseAsync_RejectsBackupOutsideBackupDirectoryAndPreservesCache()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "swgoh-command-bridge-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);

        try
        {
            var databasePath = Path.Combine(testDirectory, "cache.db");
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.InitializeDatabase();
            context.Players.Add(new PlayerEntity
            {
                AllyCode = "123456789",
                Name = "Protected Player"
            });
            await context.SaveChangesAsync();

            var outsidePath = Path.Combine(testDirectory, "outside.db");
            await File.WriteAllTextAsync(outsidePath, "not a cache");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.RestoreDatabaseAsync(outsidePath));

            Assert.Contains("backup directory", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Protected Player", (await context.Players.SingleAsync()).Name);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    private static PlayerEntity CreatePlayer(string name, int priority)
    {
        var player = new PlayerEntity
        {
            AllyCode = "123456789",
            Name = name,
            Level = 85,
            GalacticPower = 1000
        };
        player.Characters.Add(new CharacterEntity
        {
            Id = "CHARACTER",
            PlayerAllyCode = player.AllyCode,
            Name = "Character",
            Priority = priority,
            Player = player
        });
        player.Mods.Add(new GameModEntity
        {
            Id = "MOD",
            PlayerAllyCode = player.AllyCode,
            Slot = 1,
            Set = 1,
            Rarity = 5,
            Player = player
        });
        return player;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
