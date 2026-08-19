#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace swgoh_command_bridge.Core.Services;

public sealed record CharacterCatalogEntry(
    string Id,
    string Name,
    string PortraitAsset);

public sealed record CharacterCatalogPayload(
    IReadOnlyList<string> GameDataJsonSegments,
    string LocalizationJson,
    string Source = "Comlink")
{
    public CharacterCatalogPayload(
        string gameDataJson,
        string localizationJson,
        string source = "Comlink")
        : this([gameDataJson], localizationJson, source)
    {
    }
}

public sealed record CharacterCatalogAudit(
    int CandidateUnitRecords,
    int DuplicateIds,
    int Entries,
    int EntriesWithLocalizedNames,
    int EntriesWithDirectNames,
    int EntriesWithPortraits,
    IReadOnlyList<string> MissingNameIds)
{
    public int EntriesWithNames => EntriesWithLocalizedNames + EntriesWithDirectNames;

    public string Summary =>
        $"catalog entries={Entries}, candidate records={CandidateUnitRecords}, " +
        $"duplicates={DuplicateIds}, localized names={EntriesWithLocalizedNames}, " +
        $"direct names={EntriesWithDirectNames}, portraits={EntriesWithPortraits}" +
        (MissingNameIds.Count == 0
            ? string.Empty
            : $", missing names={string.Join(", ", MissingNameIds)}");
}

public sealed record CharacterCatalogParseResult(
    IReadOnlyDictionary<string, CharacterCatalogEntry> Entries,
    CharacterCatalogAudit Audit);

/// <summary>
/// Converts Comlink's units and localization collections into the exact roster
/// ID/name/portrait mapping used by the application.
/// </summary>
public sealed class CharacterCatalogParser
{
    public IReadOnlyDictionary<string, CharacterCatalogEntry> Parse(CharacterCatalogPayload payload)
        => ParseWithAudit(payload).Entries;

    public CharacterCatalogParseResult ParseWithAudit(CharacterCatalogPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var localization = JsonDocument.Parse(payload.LocalizationJson);
        var localizedValues = ReadLocalization(localization.RootElement);
        var candidates = new Dictionary<string, List<UnitCandidate>>(StringComparer.OrdinalIgnoreCase);
        var candidateCount = 0;

        foreach (var gameDataJson in payload.GameDataJsonSegments)
        {
            using var gameData = JsonDocument.Parse(gameDataJson);
            foreach (var unit in EnumerateObjects(gameData.RootElement))
            {
                var id = GetString(unit, "baseId", "unitBaseId");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                candidateCount++;
                var normalizedId = id.Split(':', 2)[0].Trim();

                var nameKey = GetString(unit, "nameKey", "name_key");
                var localized = !string.IsNullOrWhiteSpace(nameKey) &&
                           TryGetLocalizedValue(localizedValues, nameKey, out var localizedName)
                    ? localizedName
                    : null;
                var name = localized ?? (IsDisplayName(nameKey)
                    ? nameKey
                    : GetString(unit, "name", "displayName", "localizedName"));

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = CharacterNameFormatter.Format(id);
                }

                var portrait = NormalizePortraitAsset(GetString(
                    unit,
                    "thumbnailName",
                    "thumbnail",
                    "characterImage",
                    "image",
                    "portrait"));

                var candidate = new UnitCandidate(
                    new CharacterCatalogEntry(normalizedId, name.Trim(), portrait),
                    !string.IsNullOrWhiteSpace(localized),
                    Score(name, localized, portrait));
                if (!candidates.TryGetValue(normalizedId, out var entriesForId))
                {
                    entriesForId = new List<UnitCandidate>();
                    candidates[normalizedId] = entriesForId;
                }

                entriesForId.Add(candidate);
            }
        }

        var entries = new Dictionary<string, CharacterCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        var selectedCandidates = new Dictionary<string, UnitCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, values) in candidates)
        {
            var selected = values
                .OrderByDescending(value => value.Score)
                .ThenByDescending(value => value.Entry.PortraitAsset.Length)
                .First();
            selectedCandidates[id] = selected;
            entries[id] = selected.Entry;
        }

        var missingNameIds = entries.Values
            .Where(entry => string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry => entry.Id)
            .Take(20)
            .ToArray();
        var audit = new CharacterCatalogAudit(
            candidateCount,
            candidateCount - entries.Count,
            entries.Count,
            selectedCandidates.Values.Count(value => value.IsLocalized),
            selectedCandidates.Values.Count(value => !value.IsLocalized),
            entries.Values.Count(entry => !string.IsNullOrWhiteSpace(entry.PortraitAsset)),
            missingNameIds);
        return new CharacterCatalogParseResult(entries, audit);
    }

    private static int Score(string? name, string? localizedName, string portrait) =>
        (string.IsNullOrWhiteSpace(name) ? 0 : 1) +
        (string.IsNullOrWhiteSpace(localizedName) ? 0 : 4) +
        (string.IsNullOrWhiteSpace(portrait) ? 0 : 2);

    private sealed record UnitCandidate(
        CharacterCatalogEntry Entry,
        bool IsLocalized,
        int Score);

    private static Dictionary<string, string> ReadLocalization(JsonElement root)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in EnumerateObjects(root))
        {
            var pairKey = GetString(obj, "key", "id", "name");
            var pairValue = GetString(obj, "value", "text", "translation", "localizedString");
            if (!string.IsNullOrWhiteSpace(pairKey) && !string.IsNullOrWhiteSpace(pairValue))
            {
                values[pairKey] = pairValue;
            }

            foreach (var property in obj.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values[property.Name] = value;
                    }
                }
            }
        }

        return values;
    }

    private static bool TryGetLocalizedValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        out string value)
    {
        if (values.TryGetValue(key, out value!))
        {
            return true;
        }

        var normalizedKey = NormalizeKey(key);
        foreach (var pair in values)
        {
            if (NormalizeKey(pair.Key) == normalizedKey)
            {
                value = pair.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var nested in EnumerateObjects(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in EnumerateObjects(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static string? GetString(JsonElement parent, params string[] names)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            foreach (var property in parent.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }
        }

        return null;
    }

    private static string NormalizePortraitAsset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var asset = value.Trim();
        var queryStart = asset.IndexOf('?');
        if (queryStart >= 0)
        {
            asset = asset[..queryStart];
        }

        asset = asset.Replace('\\', '/');
        var separator = asset.LastIndexOf('/');
        if (separator >= 0)
        {
            asset = asset[(separator + 1)..];
        }

        if (asset.StartsWith("tex.", StringComparison.OrdinalIgnoreCase))
        {
            asset = asset[4..];
        }

        if (!asset.StartsWith("charui_", StringComparison.OrdinalIgnoreCase))
        {
            asset = "charui_" + asset;
        }

        return asset.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? asset
            : asset + ".png";
    }

    private static bool IsDisplayName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Any(char.IsWhiteSpace) || value.Any(character => !char.IsLetterOrDigit(character) && character != '_'));

    private static string NormalizeKey(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}

public interface ICharacterCatalogService
{
    Task<CharacterCatalogPayload> FetchCharacterCatalogAsync(CancellationToken cancellationToken = default);
}
