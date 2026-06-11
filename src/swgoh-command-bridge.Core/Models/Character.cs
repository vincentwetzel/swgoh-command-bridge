using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models;

public record Character(
    string Id,             // e.g. "DARTHTRAYA"
    string Name,           // User-friendly display name
    int Level,
    int GearLevel,
    int RelicTier,         // 0 if non-relic, 1+ if relic activated
    int GalacticPower,
    int Priority,          // User-defined priority for mod assignment
    Dictionary<ModSlot, GameMod> EquippedMods
)
{
    /// <summary>
    /// Gets the current set bonuses active on this character.
    /// </summary>
    public IEnumerable<ModSet> GetActiveSets()
    {
        var counts = new Dictionary<ModSet, int>();
        foreach (var mod in EquippedMods.Values)
        {
            counts[mod.Set] = counts.GetValueOrDefault(mod.Set) + 1;
        }

        foreach (var (set, count) in counts)
        {
            int required = set switch
            {
                ModSet.Speed => 4,
                ModSet.CriticalDamage => 4,
                ModSet.Offense => 4,
                _ => 2 // Health, Defense, Crit Chance, Potency, Tenacity only need 2
            };

            int activeBonuses = count / required;
            for (int i = 0; i < activeBonuses; i++)
            {
                yield return set;
            }
        }
    }
}
