#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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
    public async Task DeletePlayerAsync_RemovesPlayerAndAccountOwnedRows()
    {
        var repository = new PlayerRepository(
            _context,
            NullLogger<PlayerRepository>.Instance);

        await repository.SavePlayerAsync(CreatePlayer("To Remove", 60));

        var removed = await repository.DeletePlayerAsync("123456789");

        Assert.True(removed);
        Assert.Empty(await _context.Players.ToListAsync());
        Assert.Empty(await _context.Characters.ToListAsync());
        Assert.Empty(await _context.Mods.ToListAsync());
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

        Assert.Equal(3L, command.ExecuteScalar());
    }

    [Fact]
    public void InitializeDatabase_ReportsAppliedMigrationsAndIsIdempotent()
    {
        _context.InitializeDatabase();

        Assert.NotNull(_context.LastSchemaMigration);
        Assert.Equal(0, _context.LastSchemaMigration!.PreviousVersion);
        Assert.Equal(3, _context.LastSchemaMigration.CurrentVersion);
        Assert.True(_context.LastSchemaMigration.Changed);
        Assert.Contains("2: mod stat snapshots", _context.LastSchemaMigration.AppliedMigrations);
        Assert.Contains("3: recommendation provenance", _context.LastSchemaMigration.AppliedMigrations);

        _context.InitializeDatabase();

        Assert.NotNull(_context.LastSchemaMigration);
        Assert.Equal(3, _context.LastSchemaMigration!.PreviousVersion);
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

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' " +
            "AND name IN ('Players', 'Characters', 'Mods', 'SwgohGgRecommendations');";
        Assert.Equal(4L, tableCommand.ExecuteScalar());
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
