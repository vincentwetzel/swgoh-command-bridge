#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>Analyzes a mod against a threshold and cached character context.</summary>
    public class ModAdvisorService : IModAdvisorService
    {
        private readonly ILogger<ModAdvisorService> _logger;
        private readonly ModMechanicsService _mechanicsService;

        public ModAdvisorService(ILogger<ModAdvisorService> logger, ModMechanicsService mechanicsService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(mechanicsService);
            _logger = logger;
            _mechanicsService = mechanicsService;
        }

        public async Task<ModRecommendation> AnalyzeModAsync(
            GameMod mod,
            ModUpgradeThreshold threshold,
            IEnumerable<Character> characters,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(mod);
            ArgumentNullException.ThrowIfNull(threshold);
            ArgumentNullException.ThrowIfNull(characters);
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("Analyzing mod {ModId} against threshold {ThresholdId}", mod.Id, threshold.Id);
            var characterList = characters.ToList();
            mod.HasSecondary(StatType.Speed, out var speedStat);
            var speed = speedStat?.Value ?? 0;
            var currentEfficiency = _mechanicsService.CalculateSecondaryEfficiency(mod);
            var projectedEfficiency = _mechanicsService.CalculatePotentialSecondaryEfficiency(mod);
            var efficiencyRequired = threshold.MinimumEfficiency > 0;

            // Rarity is a hard floor. A low-pip mod is never upgraded or sliced just
            // because its current speed happens to be high.
            if (mod.Pips < threshold.MinimumRarity)
            {
                return Result(mod, ModRecommendationAction.Sell, 10,
                    $"Sell: rarity {mod.Pips} is below the required minimum of {threshold.MinimumRarity}.",
                    threshold,
                    currentEfficiency,
                    projectedEfficiency);
            }

            if (mod.Level < 15)
            {
                var potentialSpeed = _mechanicsService.CalculatePotentialMaxSpeed(mod);
                if (threshold.UpgradeOnlyWithSpeed && potentialSpeed < threshold.MinimumSpeed)
                {
                    return Result(mod, ModRecommendationAction.Sell, 10,
                        $"Sell: this level {mod.Level} mod can reach at most {potentialSpeed:0.#} Speed, below the required {threshold.MinimumSpeed}.",
                        threshold,
                        currentEfficiency,
                        projectedEfficiency);
                }

                if (efficiencyRequired && projectedEfficiency < threshold.MinimumEfficiency)
                {
                    return Result(mod, ModRecommendationAction.Sell, 10,
                        $"Sell: projected efficiency {projectedEfficiency:0.#}% cannot reach the required {threshold.MinimumEfficiency:0.#}%.",
                        threshold,
                        currentEfficiency,
                        projectedEfficiency);
                }

                return Result(mod, ModRecommendationAction.LevelUp, 50,
                    $"Level up: the mod is level {mod.Level}/15 and has viable potential up to {potentialSpeed:0.#} Speed.",
                    threshold,
                    currentEfficiency,
                    projectedEfficiency);
            }

            var meetsSpeed = !threshold.UpgradeOnlyWithSpeed || speed >= threshold.MinimumSpeed;
            var meetsTier = mod.Tier >= threshold.MinimumTier;
            var meetsEfficiency = !efficiencyRequired || currentEfficiency >= threshold.MinimumEfficiency;
            var canReachEfficiency = !efficiencyRequired || projectedEfficiency >= threshold.MinimumEfficiency;
            if (meetsSpeed && !meetsTier && mod.Tier < 5 && canReachEfficiency)
            {
                return Result(mod, ModRecommendationAction.Slice, 75,
                    $"Slice: current Speed +{speed:0.#} meets the threshold, but tier {mod.Tier} is below the required tier {threshold.MinimumTier}.",
                    threshold,
                    currentEfficiency,
                    projectedEfficiency);
            }

            if (meetsSpeed && meetsTier && meetsEfficiency)
            {
                if (mod.Pips == 5 && mod.Tier == 5)
                {
                    return Result(mod, ModRecommendationAction.Slice, 90,
                        "Slice: a level 15, 5-dot gold mod that meets the threshold can be advanced to 6-dot.",
                        threshold,
                        currentEfficiency,
                        projectedEfficiency);
                }

                return Result(mod, ModRecommendationAction.Keep, 100,
                    $"Keep: Speed +{speed:0.#}, tier {mod.Tier}, rarity {mod.Pips}, and efficiency {currentEfficiency:0.#}% meet the active threshold.",
                    threshold,
                    currentEfficiency,
                    projectedEfficiency);
            }

            var slicePotential = _mechanicsService.CalculatePotentialMaxSpeed(mod);
            if (mod.Tier < 5 && slicePotential >= threshold.MinimumSpeed && threshold.UpgradeOnlyWithSpeed && canReachEfficiency)
            {
                return Result(mod, ModRecommendationAction.Slice, 80,
                    $"Slice: current Speed +{speed:0.#} is below {threshold.MinimumSpeed}, but slicing can reach up to +{slicePotential:0.#}.",
                    threshold,
                    currentEfficiency,
                    projectedEfficiency);
            }

            var swap = FindBestSwap(mod, characterList, speed);
            if (swap != null)
            {
                return Result(mod, ModRecommendationAction.Swap, 80,
                    $"Swap: +{speed:0.#} Speed beats {swap.Value.EquippedSpeed:0.#} on {swap.Value.Character.Name} ({swap.Value.Character.Id}), the highest-priority compatible target ({FormatRosterContext(swap.Value.Character)}).",
                    threshold,
                    currentEfficiency,
                    projectedEfficiency);
            }

            return Result(mod, ModRecommendationAction.Sell, 10,
                $"Sell: Speed +{speed:0.#}/{threshold.MinimumSpeed}, efficiency {currentEfficiency:0.#}%, and no compatible higher-priority replacement target was found.",
                threshold,
                currentEfficiency,
                projectedEfficiency);
        }

        private static ModRecommendation Result(
            GameMod mod,
            ModRecommendationAction action,
            double score,
            string reason,
            ModUpgradeThreshold threshold,
            double currentEfficiency,
            double projectedEfficiency) => new(mod.Id, action, reason, score)
            {
                CurrentEfficiency = currentEfficiency,
                ProjectedEfficiency = projectedEfficiency,
                ThresholdId = threshold.Id,
                ThresholdName = threshold.Name
            };

        private static (Character Character, double EquippedSpeed)? FindBestSwap(
            GameMod mod,
            IEnumerable<Character> characters,
            double candidateSpeed)
        {
            var selected = characters
                .Where(character => !string.Equals(character.Id, mod.EquippedUnitId, StringComparison.Ordinal))
                .Where(character => character.EquippedMods.TryGetValue(mod.Slot, out var equipped) &&
                    equipped.Set == mod.Set &&
                    equipped.Primary.Type == mod.Primary.Type)
                .Select(character =>
                {
                    var equipped = character.EquippedMods[mod.Slot];
                    equipped.HasSecondary(StatType.Speed, out var equippedSpeedStat);
                    return (Character: character, EquippedSpeed: equippedSpeedStat?.Value ?? 0);
                })
                .Where(candidate => candidateSpeed > candidate.EquippedSpeed)
                .OrderByDescending(candidate => candidate.Character.Priority)
                .ThenByDescending(candidate => candidate.Character.RelicTier)
                .ThenByDescending(candidate => candidate.Character.GearLevel)
                .ThenByDescending(candidate => candidate.Character.GalacticPower)
                .ThenBy(candidate => candidate.Character.Name, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Character.Id, StringComparer.Ordinal)
                .FirstOrDefault();

            return selected.Character == null ? null : selected;
        }

        private static string FormatRosterContext(Character character) =>
            $"priority {character.Priority}, relic {character.RelicTier}, gear {character.GearLevel}, GP {character.GalacticPower:N0}";
    }
}
