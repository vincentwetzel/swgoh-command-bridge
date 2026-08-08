#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Converts persisted cache entities into the domain models consumed by advisor and UI services.
/// </summary>
public static class PersistedModelMapper
{
    public static Character ToCharacter(
        CharacterEntity character,
        IEnumerable<GameModEntity> equippedMods)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(equippedMods);

        var equipped = new Dictionary<ModSlot, GameMod>();
        foreach (var mod in equippedMods)
        {
            if (!Enum.IsDefined(typeof(ModSlot), mod.Slot))
            {
                continue;
            }

            equipped[(ModSlot)mod.Slot] = ToGameMod(mod);
        }

        return new Character(
            character.Id,
            character.Name,
            character.Level,
            character.GearLevel,
            0,
            (int)Math.Clamp(character.GalacticPower, 0L, int.MaxValue),
            character.Priority,
            equipped)
        {
            Stars = character.Stars
        };
    }

    public static GameMod ToGameMod(GameModEntity mod)
    {
        ArgumentNullException.ThrowIfNull(mod);

        var primaryType = Enum.TryParse<StatType>(mod.PrimaryStatType, true, out var parsedType)
            ? parsedType
            : StatType.None;
        var secondaries = new List<ModStat>();

        try
        {
            var snapshots = JsonSerializer.Deserialize<List<ModStatSnapshot>>(mod.SecondaryStatsJson);
            if (snapshots != null)
            {
                foreach (var snapshot in snapshots)
                {
                    if (Enum.TryParse<StatType>(snapshot.Type, true, out var secondaryType))
                    {
                        secondaries.Add(new ModStat(secondaryType, snapshot.Value, snapshot.RollCount));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // A malformed cached stat payload should not prevent inventory browsing.
        }

        return new GameMod(
            mod.Id,
            mod.Level,
            mod.Rarity,
            mod.Tier,
            (ModSlot)mod.Slot,
            (ModSet)mod.Set,
            new ModStat(primaryType, mod.PrimaryStatValue),
            secondaries,
            string.IsNullOrWhiteSpace(mod.CharacterId) ? null : mod.CharacterId);
    }
}
