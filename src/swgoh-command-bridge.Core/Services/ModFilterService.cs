#nullable enable

using System;
using System.Collections.Generic;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Performance-optimized service for filtering large lists of mods without heavy LINQ allocations.
    /// </summary>
    public class ModFilterService
    {
        /// <summary>
        /// Filters a list of game mods using traditional index-based for loops to avoid heap allocations.
        /// </summary>
        public List<GameMod> FilterMods(IEnumerable<GameMod> mods, ModFilterCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(mods);
            ArgumentNullException.ThrowIfNull(criteria);

            // Convert input to list once to support efficient indexing (Rule 18)
            var list = mods as List<GameMod> ?? new List<GameMod>(mods);
            var results = new List<GameMod>(list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                var mod = list[i];

                // 1. Equipped Status Filter
                if (criteria.IsEquipped.HasValue)
                {
                    bool isEquipped = mod.IsEquipped;
                    if (criteria.IsEquipped.Value != isEquipped)
                    {
                        continue;
                    }
                }

                // 2. Slot Filter
                if (criteria.Slots != null && criteria.Slots.Count > 0)
                {
                    bool match = false;
                    for (int j = 0; j < criteria.Slots.Count; j++)
                    {
                        if (mod.Slot == criteria.Slots[j])
                        {
                            match = true;
                            break;
                        }
                    }
                    if (!match) continue;
                }

                // 3. Set Filter
                if (criteria.Sets != null && criteria.Sets.Count > 0)
                {
                    bool match = false;
                    for (int j = 0; j < criteria.Sets.Count; j++)
                    {
                        if (mod.Set == criteria.Sets[j])
                        {
                            match = true;
                            break;
                        }
                    }
                    if (!match) continue;
                }

                // 4. Primary Stat Filter
                if (criteria.PrimaryStats != null && criteria.PrimaryStats.Count > 0)
                {
                    bool match = false;
                    for (int j = 0; j < criteria.PrimaryStats.Count; j++)
                    {
                        if (mod.Primary.Type == criteria.PrimaryStats[j])
                        {
                            match = true;
                            break;
                        }
                    }
                    if (!match) continue;
                }

                // 5. Level Range Filter
                if (criteria.MinLevel.HasValue && mod.Level < criteria.MinLevel.Value) continue;
                if (criteria.MaxLevel.HasValue && mod.Level > criteria.MaxLevel.Value) continue;

                // 6. Pips Range Filter
                if (criteria.MinPips.HasValue && mod.Pips < criteria.MinPips.Value) continue;
                if (criteria.MaxPips.HasValue && mod.Pips > criteria.MaxPips.Value) continue;

                // 7. Tier Range Filter
                if (criteria.MinTier.HasValue && mod.Tier < criteria.MinTier.Value) continue;
                if (criteria.MaxTier.HasValue && mod.Tier > criteria.MaxTier.Value) continue;

                // 8. Secondary Stats Combination Filter (AND combination)
                if (criteria.Secondaries != null && criteria.Secondaries.Count > 0)
                {
                    bool secondariesMatch = true;
                    for (int j = 0; j < criteria.Secondaries.Count; j++)
                    {
                        var reqSec = criteria.Secondaries[j];
                        bool found = false;
                        for (int k = 0; k < mod.Secondaries.Count; k++)
                        {
                            var actualSec = mod.Secondaries[k];
                            if (actualSec.Type == reqSec.Type)
                            {
                                if (reqSec.MinValue.HasValue && actualSec.Value < reqSec.MinValue.Value) break;
                                if (reqSec.MaxValue.HasValue && actualSec.Value > reqSec.MaxValue.Value) break;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            secondariesMatch = false;
                            break;
                        }
                    }
                    if (!secondariesMatch) continue;
                }

                results.Add(mod);
            }

            return results;
        }
    }
}

namespace swgoh_command_bridge.Core.Models
{
    public record ModFilterCriteria(
        List<ModSlot>? Slots = null,
        List<ModSet>? Sets = null,
        List<StatType>? PrimaryStats = null,
        List<SecondaryStatCriteria>? Secondaries = null,
        int? MinLevel = null,
        int? MaxLevel = null,
        int? MinPips = null,
        int? MaxPips = null,
        int? MinTier = null,
        int? MaxTier = null,
        bool? IsEquipped = null
    );

    public record SecondaryStatCriteria(
        StatType Type,
        double? MinValue = null,
        double? MaxValue = null
    );
}