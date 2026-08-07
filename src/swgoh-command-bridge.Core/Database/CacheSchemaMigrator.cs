#nullable enable

using System;
using System.Collections.Generic;
using System.Data.Common;

namespace swgoh_command_bridge.Core.Database;

/// <summary>Describes a cache schema migration pass.</summary>
public sealed record CacheSchemaMigrationResult(
    int PreviousVersion,
    int CurrentVersion,
    IReadOnlyList<string> AppliedMigrations)
{
    public bool Changed => AppliedMigrations.Count > 0;
}

/// <summary>
/// Applies small, transactional SQLite compatibility migrations to the local cache.
/// </summary>
public sealed class CacheSchemaMigrator
{
    public const int CurrentVersion = 3;

    public CacheSchemaMigrationResult Migrate(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction,
                "CREATE TABLE IF NOT EXISTS \"__CacheSchema\" (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"Version\" INTEGER NOT NULL);");
            EnsureCurrentTables(connection, transaction);
            var previousVersion = ReadVersion(connection, transaction);
            if (previousVersion > CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"The local cache schema version {previousVersion} is newer than this application supports ({CurrentVersion}).");
            }

            var applied = new List<string>();
            if (previousVersion < 2)
            {
                EnsureColumns(
                    connection,
                    transaction,
                    "Mods",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["PrimaryStatType"] = "TEXT NOT NULL DEFAULT 'None'",
                        ["PrimaryStatValue"] = "REAL NOT NULL DEFAULT 0",
                        ["SecondaryStatsJson"] = "TEXT NOT NULL DEFAULT '[]'"
                    });
                applied.Add("2: mod stat snapshots");
            }

            if (previousVersion < 3)
            {
                EnsureColumns(
                    connection,
                    transaction,
                    "SwgohGgRecommendations",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Source"] = "TEXT NOT NULL DEFAULT 'swgoh.gg'",
                        ["RecommendationSchemaVersion"] = "INTEGER NOT NULL DEFAULT 1",
                        ["SourceUrl"] = "TEXT NOT NULL DEFAULT ''"
                    });
                applied.Add("3: recommendation provenance");
            }

            if (previousVersion < CurrentVersion)
            {
                Execute(
                    connection,
                    transaction,
                    $"INSERT OR REPLACE INTO \"__CacheSchema\" (\"Id\", \"Version\") VALUES (1, {CurrentVersion});");
            }

            transaction.Commit();
            return new CacheSchemaMigrationResult(
                previousVersion,
                CurrentVersion,
                applied.AsReadOnly());
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    private static int ReadVersion(DbConnection connection, DbTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT \"Version\" FROM \"__CacheSchema\" WHERE \"Id\" = 1;";
        var value = command.ExecuteScalar();
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    private static void EnsureCurrentTables(DbConnection connection, DbTransaction transaction)
    {
        Execute(connection, transaction,
            "CREATE TABLE IF NOT EXISTS \"Players\" (" +
            "\"AllyCode\" TEXT NOT NULL CONSTRAINT \"PK_Players\" PRIMARY KEY, " +
            "\"Name\" TEXT NOT NULL, \"Level\" INTEGER NOT NULL, \"GalacticPower\" INTEGER NOT NULL);");
        Execute(connection, transaction,
            "CREATE TABLE IF NOT EXISTS \"Characters\" (" +
            "\"Id\" TEXT NOT NULL, \"PlayerAllyCode\" TEXT NOT NULL, \"Name\" TEXT NOT NULL, " +
            "\"Level\" INTEGER NOT NULL, \"Stars\" INTEGER NOT NULL, \"GearLevel\" INTEGER NOT NULL, " +
            "\"GalacticPower\" INTEGER NOT NULL, \"Priority\" INTEGER NOT NULL, " +
            "CONSTRAINT \"PK_Characters\" PRIMARY KEY (\"Id\", \"PlayerAllyCode\"), " +
            "CONSTRAINT \"FK_Characters_Players_PlayerAllyCode\" FOREIGN KEY (\"PlayerAllyCode\") " +
            "REFERENCES \"Players\" (\"AllyCode\") ON DELETE CASCADE);");
        Execute(connection, transaction,
            "CREATE TABLE IF NOT EXISTS \"Mods\" (" +
            "\"Id\" TEXT NOT NULL, \"PlayerAllyCode\" TEXT NOT NULL, \"CharacterId\" TEXT NOT NULL, " +
            "\"Set\" INTEGER NOT NULL, \"Slot\" INTEGER NOT NULL, \"Level\" INTEGER NOT NULL, " +
            "\"Tier\" INTEGER NOT NULL, \"Rarity\" INTEGER NOT NULL, " +
            "\"PrimaryStatType\" TEXT NOT NULL DEFAULT 'None', \"PrimaryStatValue\" REAL NOT NULL DEFAULT 0, " +
            "\"SecondaryStatsJson\" TEXT NOT NULL DEFAULT '[]', " +
            "CONSTRAINT \"PK_Mods\" PRIMARY KEY (\"Id\", \"PlayerAllyCode\"), " +
            "CONSTRAINT \"FK_Mods_Players_PlayerAllyCode\" FOREIGN KEY (\"PlayerAllyCode\") " +
            "REFERENCES \"Players\" (\"AllyCode\") ON DELETE CASCADE);");
        Execute(connection, transaction,
            "CREATE TABLE IF NOT EXISTS \"SwgohGgRecommendations\" (" +
            "\"CharacterId\" TEXT NOT NULL CONSTRAINT \"PK_SwgohGgRecommendations\" PRIMARY KEY, " +
            "\"Source\" TEXT NOT NULL DEFAULT 'swgoh.gg', \"RecommendationSchemaVersion\" INTEGER NOT NULL DEFAULT 1, " +
            "\"SourceUrl\" TEXT NOT NULL DEFAULT '', \"PrimaryStatsJson\" TEXT NOT NULL DEFAULT '{}', " +
            "\"SetRecommendationsJson\" TEXT NOT NULL DEFAULT '[]', \"PopularityPercentage\" REAL NOT NULL, " +
            "\"LastUpdatedUtc\" TEXT NOT NULL);");
    }

    private static void EnsureColumns(
        DbConnection connection,
        DbTransaction transaction,
        string tableName,
        IReadOnlyDictionary<string, string> columns)
    {
        var existingColumns = new HashSet<string>(
            ReadColumns(connection, transaction, tableName),
            StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            if (existingColumns.Contains(column.Key))
            {
                continue;
            }

            Execute(
                connection,
                transaction,
                $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{column.Key}\" {column.Value};");
        }
    }

    private static IEnumerable<string> ReadColumns(
        DbConnection connection,
        DbTransaction transaction,
        string tableName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info('{tableName}');";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return reader.GetString(1);
        }
    }

    private static void Execute(
        DbConnection connection,
        DbTransaction transaction,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
