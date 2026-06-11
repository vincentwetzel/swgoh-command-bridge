#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Implementation of IModAdvisorService utilizing user-defined upgrade thresholds.
    /// </summary>
    public class ModAdvisorService : IModAdvisorService
    {
        private readonly ILogger<ModAdvisorService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModAdvisorService"/> class.
        /// </summary>
        public ModAdvisorService(ILogger<ModAdvisorService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<ModRecommendation> AnalyzeModAsync(
            GameModEntity mod, 
            ModUpgradeThreshold threshold, 
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(mod);
            ArgumentNullException.ThrowIfNull(threshold);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("Analyzing mod {ModId} against threshold {ThresholdId}", mod.Id, threshold.Id);

            // Rule 1: Underleveled mod needs leveling up first
            if (mod.Level < 15)
            {
                return Task.FromResult(new ModRecommendation(
                    mod.Id,
                    ModRecommendationAction.LevelUp,
                    "Mod is not level 15. Level up to reveal secondary attributes and maximize the primary stat.",
                    50.0
                ));
            }

            // Rule 2: Minimum Rarity (Stars) Check
            if (mod.Rarity < threshold.MinimumRarity)
            {
                return Task.FromResult(new ModRecommendation(
                    mod.Id,
                    ModRecommendationAction.Sell,
                    "Mod rarity is below the minimum required threshold of " + threshold.MinimumRarity + " stars.",
                    10.0
                ));
            }

            // Rule 3: 5-dot Gold mods (Rarity 5, Tier 5/A) can be sliced to 6-dot (Rarity 6)
            if (mod.Rarity == 5 && mod.Tier == 5)
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

            // Fallback default: mod is in a good, stable state
            return Task.FromResult(new ModRecommendation(
                mod.Id,
                ModRecommendationAction.Keep,
                "Mod meets all quality and tier thresholds.",
                100.0
            ));
        }
    }
}