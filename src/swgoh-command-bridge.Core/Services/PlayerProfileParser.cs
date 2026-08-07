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

    public PlayerProfile Parse(string allyCode, string rawJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allyCode);
        ArgumentNullException.ThrowIfNull(rawJson);

        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;
        var characters = new List<Character>();
        var mods = new List<GameMod>();
        var seenModIds = new HashSet<string>(StringComparer.Ordinal);

        if (TryGetArray(root, out var roster, RosterArrayNames))
        {
            foreach (var unit in roster.EnumerateArray())
            {
                if (unit.ValueKind != JsonValueKind.Object ||
                    !TryGetCharacterId(unit, out var characterId))
                {
                    continue;
                }

                var equippedMods = new Dictionary<ModSlot, GameMod>();
                foreach (var modJson in EnumerateArrayProperties(unit, "equippedStatMod", "equippedMods", "mods"))
                {
                    var parsedMod = ParseGameMod(modJson, characterId);
                    if (parsedMod == null)
                    {
                        continue;
                    }

                    equippedMods[parsedMod.Slot] = parsedMod;
                    if (seenModIds.Add(parsedMod.Id))
                    {
                        mods.Add(parsedMod);
                    }
                }

                var character = new Character(
                    Id: characterId,
                    Name: GetString(unit, "name", "characterName") ?? ToDisplayName(characterId),
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
            var ownerId = GetString(modJson, "equippedUnitId", "characterId", "ownerId");
            var parsedMod = ParseGameMod(modJson, ownerId);
            if (parsedMod != null && seenModIds.Add(parsedMod.Id))
            {
                mods.Add(parsedMod);
            }
        }

        return new PlayerProfile(
            AllyCode: allyCode,
            Name: GetString(root, "name", "playerName") ?? "Unknown",
            Level: GetInt(root, 0, "level", "playerLevel"),
            GalacticPower: GetLong(root, 0, "gp", "galacticPower"),
            Characters: characters.AsReadOnly(),
            Mods: mods.AsReadOnly());
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

        var id = GetString(modJson, "id", "modId");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var primary = ParseStat(modJson, "primaryStat", "primary") ?? new ModStat(StatType.None, 0);
        var secondaries = new List<ModStat>(4);
        foreach (var secondaryJson in EnumerateArrayProperties(modJson, "secondaryStat", "secondaryStats", "secondaries"))
        {
            var secondary = ParseStat(secondaryJson);
            if (secondary != null)
            {
                secondaries.Add(secondary with
                {
                    RollCount = GetInt(secondaryJson, 1, "roll", "rollCount")
                });
            }
        }

        var slot = Math.Clamp(GetInt(modJson, 1, "slot"), 1, 6);
        var set = Math.Max(1, GetInt(modJson, 1, "set", "setId"));
        return new GameMod(
            Id: id,
            Level: Math.Clamp(GetInt(modJson, 1, "level"), 1, 15),
            Pips: Math.Clamp(GetInt(modJson, 5, "pips", "rarity"), 1, 6),
            Tier: Math.Clamp(GetInt(modJson, 1, "tier"), 1, 5),
            Slot: (ModSlot)slot,
            Set: (ModSet)set,
            Primary: primary,
            Secondaries: secondaries,
            EquippedUnitId: string.IsNullOrWhiteSpace(equippedUnitId) ? null : equippedUnitId);
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

        var unitId = GetInt(statJson, 0, "unitId", "statId", "type");
        if (unitId == 0)
        {
            return null;
        }

        return new ModStat(
            (StatType)unitId,
            GetDouble(statJson, 0, "value", "amount") / 100000000.0,
            1);
    }

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
        var definition = GetString(unit, "definitionId", "characterId", "unitId");
        if (string.IsNullOrWhiteSpace(definition))
        {
            characterId = string.Empty;
            return false;
        }

        characterId = definition.Split(':', 2)[0];
        return characterId.Length > 0;
    }

    private static string ToDisplayName(string characterId) =>
        characterId.Replace('_', ' ').Trim();

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
