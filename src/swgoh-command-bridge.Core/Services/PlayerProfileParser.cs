#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Converts tolerant Comlink JSON variants into the domain profile used by sync and UI layers.
/// </summary>
public sealed class PlayerProfileParser
{
    private static readonly string[] RosterArrayNames = { "rosterUnit", "roster", "units" };
    private static readonly string[] InventoryArrayNames = { "mods", "mod", "inventoryMods", "modInventory", "inventory" };

    public PlayerProfile Parse(
        string allyCode,
        string rawJson,
        IReadOnlyDictionary<string, string>? characterNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allyCode);
        ArgumentNullException.ThrowIfNull(rawJson);

        using var document = JsonDocument.Parse(rawJson);
        var root = UnwrapPlayerPayload(document.RootElement);
        var characters = new List<Character>();
        var mods = new List<GameMod>();
        var seenModIds = new HashSet<string>(StringComparer.Ordinal);
        var rosterRecordsSeen = 0;
        var rosterRecordsSkipped = 0;
        var inventoryRecordsSeen = 0;
        var inventoryRecordsSkipped = 0;
        var equippedModRecordsSeen = 0;
        var equippedModRecordsSkipped = 0;
        var duplicateModsSkipped = 0;
        var warnings = new List<string>();

        if (TryGetArray(root, out var roster, RosterArrayNames))
        {
            foreach (var unit in roster.EnumerateArray())
            {
                rosterRecordsSeen++;
                if (unit.ValueKind != JsonValueKind.Object ||
                    !TryGetCharacterId(unit, out var characterId))
                {
                    rosterRecordsSkipped++;
                    continue;
                }

                var equippedMods = new Dictionary<ModSlot, GameMod>();
                foreach (var modJson in EnumerateArrayProperties(unit, "equippedStatMod", "equippedMods", "mods"))
                {
                    equippedModRecordsSeen++;
                    var parsedMod = ParseGameMod(modJson, characterId);
                    if (parsedMod == null)
                    {
                        equippedModRecordsSkipped++;
                        continue;
                    }

                    equippedMods[parsedMod.Slot] = parsedMod;
                    if (seenModIds.Add(parsedMod.Id))
                    {
                        mods.Add(parsedMod);
                    }
                    else
                    {
                        duplicateModsSkipped++;
                    }
                }

                var character = new Character(
                    Id: characterId,
                    Name: GetCharacterName(unit, characterId, characterNames),
                    Level: GetInt(unit, 1, "currentLevel", "level"),
                    GearLevel: GetInt(unit, 1, "currentGearLevel", "gearLevel"),
                    RelicTier: ParseRelicTier(unit),
                    GalacticPower: GetInt(unit, 0, "gp", "galacticPower"),
                    Priority: 0,
                    EquippedMods: equippedMods)
                {
                    Stars = GetInt(unit, 0, "currentRarity", "rarity", "stars")
                };

                characters.Add(character);
            }
        }

        foreach (var modJson in EnumerateInventoryMods(root))
        {
            inventoryRecordsSeen++;
            var ownerId = GetString(modJson, "equippedUnitId", "characterId", "ownerId");
            var parsedMod = ParseGameMod(modJson, ownerId);
            if (parsedMod == null)
            {
                inventoryRecordsSkipped++;
            }
            else if (seenModIds.Add(parsedMod.Id))
            {
                mods.Add(parsedMod);
            }
            else
            {
                duplicateModsSkipped++;
            }
        }

        if (rosterRecordsSkipped > 0)
        {
            warnings.Add($"Skipped {rosterRecordsSkipped} malformed roster record(s) out of {rosterRecordsSeen}.");
        }

        if (inventoryRecordsSkipped > 0)
        {
            warnings.Add($"Skipped {inventoryRecordsSkipped} malformed inventory mod record(s) out of {inventoryRecordsSeen}.");
        }

        if (equippedModRecordsSkipped > 0)
        {
            warnings.Add($"Skipped {equippedModRecordsSkipped} malformed equipped mod record(s) out of {equippedModRecordsSeen}.");
        }

        if (duplicateModsSkipped > 0)
        {
            warnings.Add($"Ignored {duplicateModsSkipped} duplicate mod record(s).");
        }

        return new PlayerProfile(
            AllyCode: allyCode,
            Name: GetString(root, "name", "playerName") ?? "Unknown",
            Level: GetInt(root, 0, "level", "playerLevel"),
            GalacticPower: GetLong(root, 0, "gp", "galacticPower"),
            Characters: characters.AsReadOnly(),
            Mods: mods.AsReadOnly())
        {
            Diagnostics = new PlayerSyncDiagnostics(
                rosterRecordsSeen,
                rosterRecordsSkipped,
                inventoryRecordsSeen,
                inventoryRecordsSkipped,
                duplicateModsSkipped,
                warnings.AsReadOnly())
            {
                EquippedModRecordsSeen = equippedModRecordsSeen,
                EquippedModRecordsSkipped = equippedModRecordsSkipped
            }
        };
    }

    private static JsonElement UnwrapPlayerPayload(JsonElement root)
    {
        var current = root;
        for (var depth = 0; depth < 3; depth++)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                break;
            }

            JsonElement nested = default;
            var found = false;
            foreach (var propertyName in new[] { "data", "player", "profile", "payload" })
            {
                if (TryGetProperty(current, propertyName, out var candidate) &&
                    candidate.ValueKind == JsonValueKind.Object)
                {
                    nested = candidate;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                break;
            }

            current = nested;
        }

        return current;
    }

    private static IEnumerable<JsonElement> EnumerateInventoryMods(JsonElement root)
    {
        foreach (var propertyName in InventoryArrayNames)
        {
            if (TryGetProperty(root, propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        yield return item;
                    }
                }
                else if (value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var nested in EnumerateArrayProperties(value, InventoryArrayNames))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateArrayProperties(JsonElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetArray(parent, out var array, name))
            {
                continue;
            }

            foreach (var item in array.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static GameMod? ParseGameMod(JsonElement modJson, string? equippedUnitId)
    {
        if (modJson.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = GetString(modJson, "id", "modId", "instanceId");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        // Raw Comlink mods identify their set, rarity, and slot through the
        // stat-mod definition id rather than top-level set/slot/pips fields.
        var definitionId = GetString(modJson, "definitionId", "definition");
        var definitionParts = ParseModDefinitionParts(definitionId);

        var primary = ParseStat(modJson, "primaryStat", "primary") ?? new ModStat(StatType.None, 0);
        var secondaries = new List<ModStat>(4);
        foreach (var secondaryJson in EnumerateArrayProperties(modJson, "secondaryStat", "secondaryStats", "secondaries"))
        {
            var secondary = ParseStat(secondaryJson);
            if (secondary != null)
            {
                secondaries.Add(secondary with
                {
                    RollCount = GetInt(secondaryJson, 1, "statRolls", "rollCount", "roll")
                });
            }
        }

        var slot = GetOptionalInt(modJson, "slot") ?? definitionParts.Slot;
        var set = GetOptionalInt(modJson, "set", "setId") ?? definitionParts.Set;
        var pips = GetOptionalInt(modJson, "pips", "rarity") ?? definitionParts.Pips;
        var parsedSlot = (ModSlot)Math.Clamp(slot, 1, 6);
        return new GameMod(
            Id: id,
            Level: Math.Clamp(GetInt(modJson, 1, "level"), 1, 15),
            Pips: Math.Clamp(pips, 1, 6),
            Tier: Math.Clamp(GetInt(modJson, 1, "tier"), 1, 5),
            Slot: parsedSlot,
            Set: (ModSet)Math.Max(1, set),
            Primary: primary with { Type = ModPrimaryRules.Normalize(parsedSlot, primary.Type) },
            Secondaries: secondaries,
            EquippedUnitId: string.IsNullOrWhiteSpace(equippedUnitId) ? null : equippedUnitId);
    }

    private static (int Set, int Pips, int Slot) ParseModDefinitionParts(string? definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return (1, 5, 1);
        }

        if (definitionId.Length == 3 && definitionId.All(char.IsDigit))
        {
            return (
                Math.Max(1, definitionId[0] - '0'),
                Math.Clamp(definitionId[1] - '0', 1, 6),
                Math.Clamp(definitionId[2] - '0', 1, 6));
        }

        var numericParts = definitionId
            .Split(new[] { '-', '_', ':', '|', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(3)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? (int?)value
                : null)
            .ToArray();

        if (numericParts.Length < 3 || numericParts.Any(part => !part.HasValue))
        {
            return (1, 5, 1);
        }

        return (
            Math.Max(1, numericParts[0]!.Value),
            Math.Clamp(numericParts[1]!.Value, 1, 6),
            Math.Clamp(numericParts[2]!.Value, 1, 6));
    }

    private static ModStat? ParseStat(JsonElement parent, params string[] names)
    {
        var statJson = parent;
        if (names.Length > 0)
        {
            var found = false;
            foreach (var name in names)
            {
                if (TryGetProperty(parent, name, out statJson))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return null;
            }
        }

        if (statJson.ValueKind == JsonValueKind.Object && TryGetProperty(statJson, "stat", out var nestedStat))
        {
            statJson = nestedStat;
        }

        var hasComlinkUnitStatId = TryGetProperty(statJson, "unitStatId", out _) ||
                                    TryGetProperty(statJson, "unitStat", out _);
        var unitId = GetInt(statJson, 0, "unitId", "unitStat", "unitStatId", "statId", "type");
        if (unitId == 0)
        {
            return null;
        }

        return new ModStat(
            hasComlinkUnitStatId ? MapComlinkStat(unitId) : (StatType)unitId,
            GetDouble(statJson, 0, "value", "amount", "unscaledDecimalValue", "statValueDecimal") / 100000000.0,
            1);
    }

    private static StatType MapComlinkStat(int unitStatId) => unitStatId switch
    {
        1 => StatType.Health,
        2 => StatType.Strength,
        3 => StatType.Agility,
        4 => StatType.Tactics,
        5 => StatType.Speed,
        6 => StatType.PhysicalDamage,
        7 => StatType.SpecialDamage,
        8 => StatType.Armor,
        9 => StatType.Resistance,
        10 => StatType.ArmorPenetration,
        11 => StatType.ResistancePenetration,
        12 => StatType.DodgeChance,
        13 => StatType.DeflectionChance,
        14 => StatType.PhysicalCriticalChance,
        15 => StatType.SpecialCriticalChance,
        16 => StatType.CriticalDamage,
        17 => StatType.Potency,
        18 => StatType.Tenacity,
        19 => StatType.HealthSteal,
        28 => StatType.Protection,
        41 => StatType.Offense,
        42 => StatType.Defense,
        45 => StatType.CriticalChance,
        46 => StatType.Accuracy,
        47 => StatType.CriticalAvoidance,
        48 => StatType.OffensePercent,
        49 => StatType.DefensePercent,
        53 => StatType.CriticalChancePercent,
        54 => StatType.CriticalAvoidancePercent,
        55 => StatType.HealthPercent,
        56 => StatType.ProtectionPercent,
        57 => StatType.SpeedPercent,
        _ => (StatType)unitStatId
    };

    private static int ParseRelicTier(JsonElement unit)
    {
        if (!TryGetProperty(unit, "relic", out var relic) || relic.ValueKind != JsonValueKind.Object)
        {
            return GetInt(unit, 0, "relicTier");
        }

        var rawTier = GetInt(relic, 0, "currentTier", "tier");
        return rawTier > 2 ? rawTier - 2 : 0;
    }

    private static bool TryGetCharacterId(JsonElement unit, out string characterId)
    {
        var definition = GetString(unit, "definitionId", "characterId", "unitId", "unitDefId", "baseId");
        if (string.IsNullOrWhiteSpace(definition))
        {
            definition = GetNestedObjectString(unit, "character", "unit", "metadata", "definitionId", "characterId", "unitId", "unitDefId", "baseId");
        }
        if (string.IsNullOrWhiteSpace(definition))
        {
            characterId = string.Empty;
            return false;
        }

        characterId = definition.Split(':', 2)[0];
        return characterId.Length > 0;
    }

    private static string ToDisplayName(string characterId) =>
        CharacterNameFormatter.Format(characterId);

    private static string GetCharacterName(
        JsonElement unit,
        string characterId,
        IReadOnlyDictionary<string, string>? characterNames)
    {
        if (characterNames != null && characterNames.TryGetValue(characterId, out var catalogName) &&
            !string.IsNullOrWhiteSpace(catalogName))
        {
            return catalogName;
        }

        var name = GetString(unit, "name", "characterName", "displayName", "unitName");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var nestedName = GetNestedObjectString(
            unit,
            "character",
            "unit",
            "metadata",
            "name",
            "characterName",
            "displayName",
            "unitName");
        if (!string.IsNullOrWhiteSpace(nestedName))
        {
            return nestedName;
        }

        return ToDisplayName(characterId);
    }

    private static string? GetNestedObjectString(
        JsonElement parent,
        string firstProperty,
        string secondProperty,
        string thirdProperty,
        params string[] names)
    {
        foreach (var propertyName in new[] { firstProperty, secondProperty, thirdProperty })
        {
            if (TryGetProperty(parent, propertyName, out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                var value = GetString(nested, names);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool TryGetArray(JsonElement parent, out JsonElement array, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(parent, name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                array = value;
                return true;
            }
        }

        array = default;
        return false;
    }

    private static string? GetString(JsonElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(parent, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static int GetInt(JsonElement parent, int fallback, params string[] names)
    {
        var value = GetDouble(parent, fallback, names);
        return value is >= int.MinValue and <= int.MaxValue ? (int)value : fallback;
    }

    private static int? GetOptionalInt(JsonElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(parent, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var integer))
            {
                return integer;
            }

            if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
            {
                return integer;
            }
        }

        return null;
    }

    private static long GetLong(JsonElement parent, long fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(parent, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var integer))
            {
                return integer;
            }

            if (long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
            {
                return integer;
            }
        }

        return fallback;
    }

    private static double GetDouble(JsonElement parent, double fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(parent, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return fallback;
    }

    private static bool TryGetProperty(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (parent.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in parent.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
