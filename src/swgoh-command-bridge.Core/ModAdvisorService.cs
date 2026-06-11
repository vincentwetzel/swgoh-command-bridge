using System.Collections.Generic;
using System.Linq;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core
{
    public class ModAdvisorService
    {
        public string GetModRecommendation(GameMod mod, List<Character> characters, List<ModUpgradeThreshold> thresholds)
        {
            // 1. Check if the mod is worth upgrading
            var upgradeThreshold = thresholds.FirstOrDefault(t => t.Quality == mod.Quality && t.Stat == StatType.Speed);
            if (upgradeThreshold != null)
            {
                var speedStat = mod.Secondaries.FirstOrDefault(s => s.StatType == StatType.Speed);
                if (speedStat != null && speedStat.Value >= upgradeThreshold.MinValue)
                {
                    return "Upgrade";
                }
            }

            // 2. If not worth upgrading, check for swap opportunities
            var sameTypeMods = characters
                .SelectMany(c => c.EquippedMods.Values)
                .Where(m => m.Set == mod.Set && m.Primary.StatType == mod.Primary.StatType)
                .ToList();

            var charactersByPriority = characters
                .Where(c => c.EquippedMods.Values.Any(m => m.Set == mod.Set && m.Primary.StatType == mod.Primary.StatType))
                .OrderByDescending(c => c.Priority)
                .ToList();

            foreach (var character in charactersByPriority)
            {
                var equippedMod = character.EquippedMods.Values.First(m => m.Set == mod.Set && m.Primary.StatType == mod.Primary.StatType);
                var modSpeed = mod.Secondaries.FirstOrDefault(s => s.StatType == StatType.Speed)?.Value ?? 0;
                var equippedModSpeed = equippedMod.Secondaries.FirstOrDefault(s => s.StatType == StatType.Speed)?.Value ?? 0;

                if (modSpeed > equippedModSpeed)
                {
                    return $"Swap with {character.Name}'s {equippedMod.Slot} mod.";
                }
            }

            // 3. If no swap is found, recommend selling
            return "Sell";
        }
    }
}
