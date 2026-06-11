#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests
{
    /// <summary>
    /// Unit tests verifying mod upgrade, slicing, and swap advisory logic in ModAdvisorService and ModMechanicsService.
    /// </summary>
    public class ModAdvisorServiceTests
    {
        private readonly ModMechanicsService _mechanicsService;
        private readonly ModAdvisorService _advisorService;

        public ModAdvisorServiceTests()
        {
            _mechanicsService = new ModMechanicsService();
            _advisorService = new ModAdvisorService(NullLogger<ModAdvisorService>.Instance, _mechanicsService);
        }

        [Fact]
        public void CalculatePotentialMaxSpeed_WhenModHasExistingSpeed_ReturnsCorrectPotential()
        {
            // Arrange
            var secondaries = new List<ModStat>
            {
                new ModStat(StatType.Speed, 5)
            };
            var mod = new GameMod("1", level: 1, pips: 5, tier: 1, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), secondaries, null);

            // Act
            var result = _mechanicsService.CalculatePotentialMaxSpeed(mod);

            // Assert
            // Current speed 5 + 4 remaining level rolls + 4 remaining slice rolls (total 8 * 6 = 48) + 1 (5-dot to 6-dot slice) = 54
            Assert.Equal(54.0, result);
        }

        [Fact]
        public void CalculatePotentialMaxSpeed_WhenModHasNoSpeedAndUnderFourSecondaries_ReturnsPotentialSpeedWithReveal()
        {
            // Arrange
            var secondaries = new List<ModStat>
            {
                new ModStat(StatType.Health, 300)
            };
            var mod = new GameMod("1", level: 1, pips: 5, tier: 1, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), secondaries, null);

            // Act
            var result = _mechanicsService.CalculatePotentialMaxSpeed(mod);

            // Assert
            // Starts with 1 secondary. First roll reveals Speed (6).
            // level 1 to 12 gives 4 rolls. Tier 1 to 5 gives 4 rolls. Total rolls = 8.
            // 3 reveal slots are remaining.
            // Out of 8 rolls, 3 are used to fill up to 4 secondaries (reveal speed + 2 other stats).
            // Remaining 5 rolls upgrade Speed: 6 + (5 * 6.0) + 1.0 (5-dot to 6-dot slice) = 37.0.
            Assert.Equal(37.0, result);
        }

        [Fact]
        public async Task AnalyzeModAsync_WhenUnderleveledModCannotReachThreshold_ReturnsSell()
        {
            // Arrange
            var secondaries = new List<ModStat>
            {
                new ModStat(StatType.Health, 300)
            };
            var mod = new GameMod("1", level: 1, pips: 5, tier: 5, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), secondaries, null);
            var threshold = new ModUpgradeThreshold("t1", "Strict", 5, 5, 45, true, 0.0);
            var characters = new List<Character>();

            // Act
            var recommendation = await _advisorService.AnalyzeModAsync(mod, threshold, characters);

            // Assert
            Assert.Equal(ModRecommendationAction.Sell, recommendation.Action);
            Assert.Contains("maximum potential Speed", recommendation.Reason);
        }

        [Fact]
        public async Task AnalyzeModAsync_WhenUnderleveledModCanReachThreshold_ReturnsLevelUp()
        {
            // Arrange
            var secondaries = new List<ModStat>
            {
                new ModStat(StatType.Speed, 15)
            };
            var mod = new GameMod("1", level: 1, pips: 5, tier: 1, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), secondaries, null);
            var threshold = new ModUpgradeThreshold("t1", "Balanced", 5, 1, 10, true, 0.0);
            var characters = new List<Character>();

            // Act
            var recommendation = await _advisorService.AnalyzeModAsync(mod, threshold, characters);

            // Assert
            Assert.Equal(ModRecommendationAction.LevelUp, recommendation.Action);
        }

        [Fact]
        public async Task AnalyzeModAsync_WhenModMeetsAllThresholdsAndIsGold5Dot_ReturnsSliceTo6Dot()
        {
            // Arrange
            var secondaries = new List<ModStat>
            {
                new ModStat(StatType.Speed, 20)
            };
            var mod = new GameMod("1", level: 15, pips: 5, tier: 5, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), secondaries, null);
            var threshold = new ModUpgradeThreshold("t1", "Balanced", 5, 5, 15, true, 0.0);
            var characters = new List<Character>();

            // Act
            var recommendation = await _advisorService.AnalyzeModAsync(mod, threshold, characters);

            // Assert
            Assert.Equal(ModRecommendationAction.Slice, recommendation.Action);
            Assert.Contains("Slice to 6-dot", recommendation.Reason);
        }

        [Fact]
        public async Task AnalyzeModAsync_WhenModIsBelowThresholdButIsBetterThanEquippedOnPriorityCharacter_ReturnsSwap()
        {
            // Arrange
            var candidateSecondaries = new List<ModStat>
            {
                new ModStat(StatType.Speed, 12)
            };
            var candidateMod = new GameMod("candidate", level: 15, pips: 5, tier: 5, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), candidateSecondaries, null);

            var equippedSecondaries = new List<ModStat>
            {
                new ModStat(StatType.Speed, 8)
            };
            var equippedMod = new GameMod("equipped", level: 15, pips: 5, tier: 5, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), equippedSecondaries, "CHAR1");

            var character = new Character(
                "CHAR1",
                "Darth Traya",
                85,
                12,
                0,
                15000,
                priority: 10,
                new Dictionary<ModSlot, GameMod> { { ModSlot.Square, equippedMod } }
            );

            var threshold = new ModUpgradeThreshold("t1", "Strict", 5, 5, 15, true, 0.0);
            var characters = new List<Character> { character };

            // Act
            var recommendation = await _advisorService.AnalyzeModAsync(candidateMod, threshold, characters);

            // Assert
            Assert.Equal(ModRecommendationAction.Swap, recommendation.Action);
            Assert.Contains("Upgrade/Swap candidate", recommendation.Reason);
        }

        [Fact]
        public async Task AnalyzeModAsync_WhenModMeetsAllThresholdsButIsBelowMinimumTier_ReturnsSliceToNextTier()
        {
            // Arrange
            var secondaries = new List<ModStat>
            {
                new ModStat(StatType.Speed, 10)
            };
            // Tier 1 (Grey), Level 15, Pips 5
            var mod = new GameMod("1", level: 15, pips: 5, tier: 1, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), secondaries, null);
            var threshold = new ModUpgradeThreshold("t1", "Strict", 5, 4, 10, true, 0.0);
            var characters = new List<Character>();

            // Act
            var recommendation = await _advisorService.AnalyzeModAsync(mod, threshold, characters);

            // Assert
            Assert.Equal(ModRecommendationAction.Slice, recommendation.Action);
            Assert.Contains("eligible for slicing", recommendation.Reason);
        }

        [Fact]
        public async Task AnalyzeModAsync_WhenModFailsThresholdsAndHasNoPrioritySwapValue_ReturnsSell()
        {
            // Arrange
            var secondaries = new List<ModStat>
            {
                new ModStat(StatType.Speed, 2)
            };
            var mod = new GameMod("1", level: 15, pips: 5, tier: 5, ModSlot.Square, ModSet.Health, new ModStat(StatType.Offense, 0.5), secondaries, null);
            var threshold = new ModUpgradeThreshold("t1", "Strict", 5, 5, 15, true, 0.0);
            var characters = new List<Character>();

            // Act
            var recommendation = await _advisorService.AnalyzeModAsync(mod, threshold, characters);

            // Assert
            Assert.Equal(ModRecommendationAction.Sell, recommendation.Action);
            Assert.Contains("fails to meet threshold", recommendation.Reason);
        }
    }
}