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
/// Reads a catalog and accepts a locally-imported snapshot after it has been
/// completely validated. The embedded catalog remains the safe fallback.
/// </summary>
public interface ICharacterCatalogSnapshotService : ICharacterCatalogService
{
    CharacterCatalogSnapshotInfo GetSnapshotInfo();

    Task<CharacterCatalogSnapshotInfo> ImportAsync(
        string charactersPath,
        string shipsPath,
        CancellationToken cancellationToken = default);

    Task<CharacterCatalogSnapshotInfo> UpdateFromCatalogAsync(
        CharacterCatalogPayload payload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Supplies versioned SWGOH.GG character and ship catalog records keyed only
/// by their game base ID. A verified local snapshot takes precedence over the
/// embedded snapshot so catalog updates do not require rebuilding the app.
/// </summary>
public sealed class BundledCharacterCatalogService : ICharacterCatalogSnapshotService
{
    private const string CharactersFileName = "swgoh-characters.json";
    private const string ShipsFileName = "swgoh-ships.json";
    private const string ManifestFileName = "manifest.json";
    private const string ComlinkCatalogFileName = "comlink-catalog.json";
    private const string ComlinkManifestFileName = "comlink-manifest.json";
    private const int ManifestVersion = 1;
    private const int MinimumCharacterRecords = 100;
    private const int MinimumShipRecords = 20;

    private readonly string _snapshotDirectory;

    public BundledCharacterCatalogService(string? snapshotDirectory = null)
    {
        _snapshotDirectory = snapshotDirectory ?? AppDataPaths.CharacterCatalogDirectory;
    }

    public Task<CharacterCatalogPayload> FetchCharacterCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = TryReadLocalSnapshot(cancellationToken, out var local)
            ? local
            : ReadEmbeddedSnapshot(cancellationToken);
        return Task.FromResult(ToPayload(snapshot));
    }

    public CharacterCatalogSnapshotInfo GetSnapshotInfo()
    {
        if (TryReadLocalSnapshot(CancellationToken.None, out var imported))
        {
            return imported.Info;
        }

        return ReadEmbeddedSnapshot(CancellationToken.None).Info;
    }

    public async Task<CharacterCatalogSnapshotInfo> ImportAsync(
        string charactersPath,
        string shipsPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedCharactersPath = ValidateImportPath(charactersPath, "character");
        var normalizedShipsPath = ValidateImportPath(shipsPath, "ship");
        if (string.Equals(normalizedCharactersPath, normalizedShipsPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Choose separate character and ship catalog files.");
        }

        var charactersJson = await File.ReadAllBytesAsync(normalizedCharactersPath, cancellationToken)
            .ConfigureAwait(false);
        var shipsJson = await File.ReadAllBytesAsync(normalizedShipsPath, cancellationToken)
            .ConfigureAwait(false);
        var characters = ReadCatalog(charactersJson, "imported character catalog", cancellationToken);
        var ships = ReadCatalog(shipsJson, "imported ship catalog", cancellationToken);
        ValidateCatalogCounts(characters, ships);
        CombineEntries(characters, ships);

        var manifest = new CatalogSnapshotManifest(
            ManifestVersion,
            DateTimeOffset.UtcNow,
            characters.Count,
            ships.Count,
            Convert.ToHexString(SHA256.HashData(charactersJson)),
            Convert.ToHexString(SHA256.HashData(shipsJson)));
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);

        Directory.CreateDirectory(_snapshotDirectory);
        await WriteAtomicallyAsync(Path.Combine(_snapshotDirectory, CharactersFileName), charactersJson, cancellationToken)
            .ConfigureAwait(false);
        await WriteAtomicallyAsync(Path.Combine(_snapshotDirectory, ShipsFileName), shipsJson, cancellationToken)
            .ConfigureAwait(false);
        // The manifest is written last. Its hashes make it the commit marker
        // for the two-file snapshot.
        await WriteAtomicallyAsync(Path.Combine(_snapshotDirectory, ManifestFileName), manifestJson, cancellationToken)
            .ConfigureAwait(false);

        if (!TryReadImportedSnapshot(cancellationToken, out var imported))
        {
            throw new InvalidDataException("The imported catalog could not be verified after it was saved.");
        }

        return imported.Info;
    }

    public async Task<CharacterCatalogSnapshotInfo> UpdateFromCatalogAsync(
        CharacterCatalogPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        var parsed = new CharacterCatalogParser().ParseWithAudit(payload);
        if (parsed.Audit.Entries < MinimumCharacterRecords ||
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
                    image = entry.PortraitAsset
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
        ValidateCatalogCounts(characters, ships);
        return new CatalogSnapshot(
            CombineEntries(characters, ships),
            new CharacterCatalogSnapshotInfo(
                "Bundled SWGOH catalog",
                "Embedded in the application",
                characters.Count,
                ships.Count,
                null));
    }

    private bool TryReadImportedSnapshot(
        CancellationToken cancellationToken,
        out CatalogSnapshot snapshot)
    {
        snapshot = default!;
        try
        {
            var charactersPath = Path.Combine(_snapshotDirectory, CharactersFileName);
            var shipsPath = Path.Combine(_snapshotDirectory, ShipsFileName);
            var manifestPath = Path.Combine(_snapshotDirectory, ManifestFileName);
            if (!File.Exists(charactersPath) || !File.Exists(shipsPath) || !File.Exists(manifestPath))
            {
                return false;
            }

            var charactersJson = File.ReadAllBytes(charactersPath);
            var shipsJson = File.ReadAllBytes(shipsPath);
            var manifest = JsonSerializer.Deserialize<CatalogSnapshotManifest>(File.ReadAllBytes(manifestPath));
            if (manifest == null ||
                manifest.Version != ManifestVersion ||
                !string.Equals(manifest.CharactersSha256, Convert.ToHexString(SHA256.HashData(charactersJson)), StringComparison.Ordinal) ||
                !string.Equals(manifest.ShipsSha256, Convert.ToHexString(SHA256.HashData(shipsJson)), StringComparison.Ordinal))
            {
                return false;
            }

            var characters = ReadCatalog(charactersJson, "imported character catalog", cancellationToken);
            var ships = ReadCatalog(shipsJson, "imported ship catalog", cancellationToken);
            ValidateCatalogCounts(characters, ships);
            if (manifest.CharacterCount != characters.Count || manifest.ShipCount != ships.Count)
            {
                return false;
            }

            snapshot = new CatalogSnapshot(
                CombineEntries(characters, ships),
                new CharacterCatalogSnapshotInfo(
                    "Local SWGOH catalog snapshot",
                    _snapshotDirectory,
                    characters.Count,
                    ships.Count,
                    manifest.ImportedAtUtc));
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

    private bool TryReadLocalSnapshot(
        CancellationToken cancellationToken,
        out CatalogSnapshot snapshot)
    {
        var hasImported = TryReadImportedSnapshot(cancellationToken, out var imported);
        var hasComlink = TryReadComlinkSnapshot(cancellationToken, out var comlink);
        if (!hasImported && !hasComlink)
        {
            snapshot = default!;
            return false;
        }

        if (!hasImported)
        {
            snapshot = comlink;
            return true;
        }

        if (!hasComlink)
        {
            snapshot = imported;
            return true;
        }

        snapshot = comlink.Info.ImportedAtUtc >= imported.Info.ImportedAtUtc
            ? comlink
            : imported;
        return true;
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
            if (entries.Count < MinimumCharacterRecords || manifest.EntryCount != entries.Count)
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
                    thumbnailName = entry.Image
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
                GetRequiredString(value, "image", source)));
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

    private static void ValidateCatalogCounts(
        IReadOnlyCollection<CatalogEntry> characters,
        IReadOnlyCollection<CatalogEntry> ships)
    {
        if (characters.Count < MinimumCharacterRecords)
        {
            throw new InvalidDataException(
                $"The character catalog contains {characters.Count} records; at least {MinimumCharacterRecords} are required.");
        }

        if (ships.Count < MinimumShipRecords)
        {
            throw new InvalidDataException(
                $"The ship catalog contains {ships.Count} records; at least {MinimumShipRecords} are required.");
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

    private static string ValidateImportPath(string path, string catalogKind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidDataException($"Enter the path to the {catalogKind} catalog JSON file.");
        }

        var normalized = Path.GetFullPath(path.Trim());
        if (!File.Exists(normalized))
        {
            throw new FileNotFoundException($"The {catalogKind} catalog JSON file was not found.", normalized);
        }

        return normalized;
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

    private sealed record CatalogEntry(string Id, string Name, string Image);

    private sealed record CatalogSnapshot(
        IReadOnlyDictionary<string, CatalogEntry> Entries,
        CharacterCatalogSnapshotInfo Info);

    private sealed record CatalogSnapshotManifest(
        int Version,
        DateTimeOffset ImportedAtUtc,
        int CharacterCount,
        int ShipCount,
        string CharactersSha256,
        string ShipsSha256);

    private sealed record AggregateCatalogSnapshotManifest(
        int Version,
        DateTimeOffset UpdatedAtUtc,
        int EntryCount,
        string CatalogSha256);
}
