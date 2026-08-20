#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Represents the catalog currently used to enrich roster records.
/// </summary>
public sealed record CharacterCatalogSnapshotInfo(
    string Source,
    string Directory,
    int CharacterCount,
    int ShipCount,
    DateTimeOffset? ImportedAtUtc)
{
    public string Summary => ImportedAtUtc is DateTimeOffset importedAt
        ? ShipCount > 0
            ? $"{Source}: {CharacterCount} characters, {ShipCount} ships; imported {importedAt.ToLocalTime():yyyy-MM-dd HH:mm} local."
            : $"{Source}: {CharacterCount} catalog records; updated {importedAt.ToLocalTime():yyyy-MM-dd HH:mm} local."
        : $"{Source}: {CharacterCount} characters, {ShipCount} ships.";
}

/// <summary>
/// Reads the embedded catalog and manages the validated snapshot refreshed
/// automatically from Comlink. The embedded catalog remains the safe fallback.
/// </summary>
public interface ICharacterCatalogSnapshotService : ICharacterCatalogService
{
    CharacterCatalogSnapshotInfo GetSnapshotInfo();

    Task<CharacterCatalogSnapshotInfo> UpdateFromCatalogAsync(
        CharacterCatalogPayload payload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Supplies versioned catalog records keyed only by their game base ID. A
/// verified Comlink snapshot takes precedence over the embedded snapshot so
/// catalog updates do not require rebuilding the app.
/// </summary>
public sealed class BundledCharacterCatalogService : ICharacterCatalogSnapshotService
{
    private const string CharactersFileName = "swgoh-characters.json";
    private const string ShipsFileName = "swgoh-ships.json";
    private const string ComlinkCatalogFileName = "comlink-catalog.json";
    private const string ComlinkManifestFileName = "comlink-manifest.json";
    private const int ManifestVersion = 2;
    private const int MinimumComlinkCatalogRecords = 100;
    private const int MinimumEmbeddedCharacterRecords = 100;
    private const int MinimumEmbeddedShipRecords = 20;

    private readonly string _snapshotDirectory;

    public BundledCharacterCatalogService(string? snapshotDirectory = null)
    {
        _snapshotDirectory = snapshotDirectory ?? AppDataPaths.CharacterCatalogDirectory;
    }

    public Task<CharacterCatalogPayload> FetchCharacterCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = TryReadComlinkSnapshot(cancellationToken, out var local)
            ? local
            : ReadEmbeddedSnapshot(cancellationToken);
        return Task.FromResult(ToPayload(snapshot));
    }

    public CharacterCatalogSnapshotInfo GetSnapshotInfo()
    {
        if (TryReadComlinkSnapshot(CancellationToken.None, out var imported))
        {
            return imported.Info;
        }

        return ReadEmbeddedSnapshot(CancellationToken.None).Info;
    }

    public async Task<CharacterCatalogSnapshotInfo> UpdateFromCatalogAsync(
        CharacterCatalogPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        var parsed = new CharacterCatalogParser().ParseWithAudit(payload);
        if (parsed.Audit.Entries < MinimumComlinkCatalogRecords ||
            parsed.Audit.DuplicateIds != 0 ||
            parsed.Audit.EntriesWithNames != parsed.Audit.Entries ||
            parsed.Audit.EntriesWithPortraits != parsed.Audit.Entries)
        {
            throw new InvalidDataException(
                $"Comlink catalog did not pass validation: {parsed.Audit.Summary}");
        }

        var catalogJson = JsonSerializer.SerializeToUtf8Bytes(
            parsed.Entries.Values
                .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new
                {
                    base_id = entry.Id,
                    name = entry.Name,
                    image = entry.PortraitAsset,
                    alignment = entry.Alignment
                }));
        var manifest = new AggregateCatalogSnapshotManifest(
            ManifestVersion,
            DateTimeOffset.UtcNow,
            parsed.Audit.Entries,
            Convert.ToHexString(SHA256.HashData(catalogJson)));

        Directory.CreateDirectory(_snapshotDirectory);
        await WriteAtomicallyAsync(
                Path.Combine(_snapshotDirectory, ComlinkCatalogFileName),
                catalogJson,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteAtomicallyAsync(
                Path.Combine(_snapshotDirectory, ComlinkManifestFileName),
                JsonSerializer.SerializeToUtf8Bytes(manifest),
                cancellationToken)
            .ConfigureAwait(false);

        if (!TryReadComlinkSnapshot(cancellationToken, out var imported))
        {
            throw new InvalidDataException("The Comlink catalog could not be verified after it was saved.");
        }

        return imported.Info;
    }

    private CatalogSnapshot ReadEmbeddedSnapshot(CancellationToken cancellationToken)
    {
        var characters = ReadEmbeddedCatalog(CharactersFileName, cancellationToken);
        var ships = ReadEmbeddedCatalog(ShipsFileName, cancellationToken);
        ValidateEmbeddedCatalogCounts(characters, ships);
        return new CatalogSnapshot(
            CombineEntries(characters, ships),
            new CharacterCatalogSnapshotInfo(
                "Bundled SWGOH catalog",
                "Embedded in the application",
                characters.Count,
                ships.Count,
                null));
    }

    private bool TryReadComlinkSnapshot(
        CancellationToken cancellationToken,
        out CatalogSnapshot snapshot)
    {
        snapshot = default!;
        try
        {
            var catalogPath = Path.Combine(_snapshotDirectory, ComlinkCatalogFileName);
            var manifestPath = Path.Combine(_snapshotDirectory, ComlinkManifestFileName);
            if (!File.Exists(catalogPath) || !File.Exists(manifestPath))
            {
                return false;
            }

            var catalogJson = File.ReadAllBytes(catalogPath);
            var manifest = JsonSerializer.Deserialize<AggregateCatalogSnapshotManifest>(File.ReadAllBytes(manifestPath));
            if (manifest == null ||
                manifest.Version != ManifestVersion ||
                !string.Equals(manifest.CatalogSha256, Convert.ToHexString(SHA256.HashData(catalogJson)), StringComparison.Ordinal))
            {
                return false;
            }

            var entries = ReadCatalog(catalogJson, "stored Comlink catalog", cancellationToken);
            if (entries.Count < MinimumComlinkCatalogRecords || manifest.EntryCount != entries.Count)
            {
                return false;
            }

            snapshot = new CatalogSnapshot(
                ToUniqueEntries(entries),
                new CharacterCatalogSnapshotInfo(
                    "Comlink catalog snapshot",
                    _snapshotDirectory,
                    entries.Count,
                    0,
                    manifest.UpdatedAtUtc));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static CharacterCatalogPayload ToPayload(CatalogSnapshot snapshot)
    {
        var gameDataJson = JsonSerializer.Serialize(new
        {
            unit = snapshot.Entries.Values
                .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new
                {
                    baseId = entry.Id,
                    name = entry.Name,
                    thumbnailName = entry.Image,
                    alignment = entry.Alignment
                })
        });
        return new CharacterCatalogPayload(gameDataJson, "{}", snapshot.Info.Source);
    }

    private static IReadOnlyList<CatalogEntry> ReadEmbeddedCatalog(
        string fileName,
        CancellationToken cancellationToken)
    {
        var assembly = typeof(BundledCharacterCatalogService).Assembly;
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(name =>
            name.EndsWith($".Assets.CharacterCatalog.{fileName}", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            throw new InvalidDataException($"Bundled catalog resource '{fileName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"Bundled catalog resource '{fileName}' could not be opened.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return ReadCatalog(memory.ToArray(), $"bundled catalog resource '{fileName}'", cancellationToken);
    }

    private static IReadOnlyList<CatalogEntry> ReadCatalog(
        ReadOnlyMemory<byte> json,
        string source,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"The {source} must be a JSON array.");
        }

        var entries = new List<CatalogEntry>();
        foreach (var value in document.RootElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new CatalogEntry(
                GetRequiredString(value, "base_id", source),
                GetRequiredString(value, "name", source),
                GetRequiredString(value, "image", source),
                GetOptionalString(value, "alignment") ?? "Neutral"));
        }

        return entries;
    }

    private static IReadOnlyDictionary<string, CatalogEntry> CombineEntries(
        IReadOnlyList<CatalogEntry> characters,
        IReadOnlyList<CatalogEntry> ships)
    {
        return ToUniqueEntries(characters.Concat(ships));
    }

    private static IReadOnlyDictionary<string, CatalogEntry> ToUniqueEntries(
        IEnumerable<CatalogEntry> source)
    {
        var entries = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in source)
        {
            if (!entries.TryAdd(entry.Id, entry))
            {
                throw new InvalidDataException(
                    $"Catalog contains duplicate base ID '{entry.Id}'.");
            }
        }

        return entries;
    }

    private static void ValidateEmbeddedCatalogCounts(
        IReadOnlyCollection<CatalogEntry> characters,
        IReadOnlyCollection<CatalogEntry> ships)
    {
        if (characters.Count < MinimumEmbeddedCharacterRecords)
        {
            throw new InvalidDataException(
                $"The character catalog contains {characters.Count} records; at least {MinimumEmbeddedCharacterRecords} are required.");
        }

        if (ships.Count < MinimumEmbeddedShipRecords)
        {
            throw new InvalidDataException(
                $"The ship catalog contains {ships.Count} records; at least {MinimumEmbeddedShipRecords} are required.");
        }
    }

    private static string GetRequiredString(JsonElement record, string propertyName, string source)
    {
        if (record.ValueKind == JsonValueKind.Object &&
            record.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            return property.GetString()!.Trim();
        }

        throw new InvalidDataException($"The {source} contains a record without '{propertyName}'.");
    }

    private static string? GetOptionalString(JsonElement record, string propertyName)
    {
        return record.ValueKind == JsonValueKind.Object &&
               record.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;
    }

    private static async Task WriteAtomicallyAsync(
        string destinationPath,
        byte[] contents,
        CancellationToken cancellationToken)
    {
        var temporaryPath = destinationPath + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, contents, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record CatalogEntry(string Id, string Name, string Image, string Alignment);

    private sealed record CatalogSnapshot(
        IReadOnlyDictionary<string, CatalogEntry> Entries,
        CharacterCatalogSnapshotInfo Info);

    private sealed record AggregateCatalogSnapshotManifest(
        int Version,
        DateTimeOffset UpdatedAtUtc,
        int EntryCount,
        string CatalogSha256);
}
