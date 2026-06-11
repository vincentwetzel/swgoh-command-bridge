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
    /// <summary>
    /// Implementation of IModAdvisorService utilizing user-defined upgrade thresholds.
    /// </summary>
    public class ModAdvisorService : IModAdvisorService
    {
        private readonly ILogger<ModAdvisorService> _logger;
        private readonly ModMechanicsService _mechanicsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModAdvisorService"/> class.
        /// </summary>
        public ModAdvisorService(ILogger<ModAdvisorService> logger, ModMechanicsService mechanicsService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(mechanicsService);
            _logger = logger;
            _mechanicsService = mechanicsService;
        }

        /// <inheritdoc />
        public Task<ModRecommendation> AnalyzeModAsync(
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

            mod.HasSecondary(StatType.Speed, out var speedStat);
            double speedValue = speedStat?.Value ?? 0.0;

            // Rule 1: Underleveled mod needs leveling up first
            if (mod.Level < 15)
            {
                double potentialMaxSpeed = _mechanicsService.CalculatePotentialMaxSpeed(mod);

                // Save the player's credits: If the threshold requires speed, but this mod has no potential to reach it, recommend Sell
                if (threshold.UpgradeOnlyWithSpeed && potentialMaxSpeed < threshold.MinimumSpeed)
                {
                    return Task.FromResult(new ModRecommendation(
                        mod.Id,
                        ModRecommendationAction.Sell,
                        $"Mod is underleveled, but its maximum potential Speed ({potentialMaxSpeed}) cannot reach the required minimum of {threshold.MinimumSpeed}.",
                        10.0
                    ));
                }

                return Task.FromResult(new ModRecommendation(
                    mod.Id,
                    ModRecommendationAction.LevelUp,
                    "Mod is not level 15. Level up to reveal secondary attributes and maximize the primary stat.",
                    50.0
                ));
            }

            // Rule 2: Minimum Rarity (Stars) Check
            if (mod.Pips < threshold.MinimumRarity)
            {
                return Task.FromResult(new ModRecommendation(
                    mod.Id,
                    ModRecommendationAction.Sell,
                    $"Mod rarity ({mod.Pips}) is below the minimum required threshold of {threshold.MinimumRarity} stars.",
                    10.0
                ));
            }

            bool meetsTier = mod.Tier >= threshold.MinimumTier;
            bool meetsSpeed = speedValue >= threshold.MinimumSpeed;
            bool meetsThreshold = meetsTier && meetsSpeed;

            if (threshold.UpgradeOnlyWithSpeed && speedValue == 0)
            {
                meetsThreshold = false;
            }

            if (meetsThreshold)
            {
                // Rule 3: 5-dot Gold mods (Pips 5, Tier 5/A) can be sliced to 6-dot (Pips 6)
                if (mod.Pips == 5 && mod.Tier == 5)
                {
                    return Task.FromResult(new ModRecommendation(
                        mod.Id,
                        ModRecommendationAction.Slice,
                        "Mod is level 15 and Tier 5 (A). Slice to 6-dot (Rarity 6) to significantly upgrade its primary and secondary stats.",
                        90.0
                    ));
                }

                // Rule 4: Standard tier slicing (E -> D -> C -> B -> A)
                if (mod.Tier < 5 && mod.Tier < threshold.MinimumTier)
                {
                    return Task.FromResult(new ModRecommendation(
                        mod.Id,
                        ModRecommendationAction.Slice,
                        "Mod tier is eligible for slicing to improve secondary stat growth.",
                        75.0
                    ));
                }

                return Task.FromResult(new ModRecommendation(
                    mod.Id,
                    ModRecommendationAction.Keep,
                    "Mod meets all quality and tier thresholds.",
                    100.0
                ));
            }

            // Potential evaluation: If it is below current speed threshold, but potential speed after slicing meets it, recommend slice
            double potentialSpeed = _mechanicsService.CalculatePotentialMaxSpeed(mod);
            if (mod.Pips >= threshold.MinimumRarity && potentialSpeed >= threshold.MinimumSpeed && mod.Tier < 5)
            {
                return Task.FromResult(new ModRecommendation(
                    mod.Id,
                    ModRecommendationAction.Slice,
                    $"Mod's current Speed (+{speedValue}) is below threshold, but potential Speed after slicing (+{potentialSpeed}) meets the requirement. Slice to upgrade.",
                    80.0
                ));
            }

            // Rule 5: Swap Candidate Check (Rule 18 performance manual looping optimization)
            var sortedCharacters = characters.OrderByDescending(c => c.Priority).ToList();
            for (int i = 0; i < sortedCharacters.Count; i++)
            {
                var character = sortedCharacters[i];
                if (character.EquippedMods.TryGetValue(mod.Slot, out var equippedMod))
                {
                    if (equippedMod.Set == mod.Set && equippedMod.Primary.Type == mod.Primary.Type)
                    {
                        equippedMod.HasSecondary(StatType.Speed, out var equippedSpeedStat);
                        double equippedSpeed = equippedSpeedStat?.Value ?? 0.0;

                        if (speedValue > equippedSpeed)
                        {
                            return Task.FromResult(new ModRecommendation(
                                mod.Id,
                                ModRecommendationAction.Swap,
                                $"Upgrade/Swap candidate: Higher speed (+{speedValue}) than mod currently equipped on high-priority character {character.Name} (+{equippedSpeed}).",
                                80.0
                            ));
                        }
                    }
                }
            }

            // Fallback default: If not meeting thresholds and not viable as an upgrade swap, recommend Sell
            return Task.FromResult(new ModRecommendation(
                mod.Id,
                ModRecommendationAction.Sell,
                $"Mod fails to meet threshold (Speed: {speedValue}/{threshold.MinimumSpeed}, Rarity: {mod.Pips}/{threshold.MinimumRarity}) and has no high-priority swap value.",
                10.0
            ));
        }
    }
}