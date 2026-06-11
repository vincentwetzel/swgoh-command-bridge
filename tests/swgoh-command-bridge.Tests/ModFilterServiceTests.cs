#nullable enable

using System.Collections.Generic;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests
{
    /// <summary>
    /// Unit tests verifying high-performance filtering options in ModFilterService.
    /// </summary>
    public class ModFilterServiceTests
    {
        [Fact]
        public void FilterMods_WithEquippedAndSlotCriteria_ReturnsOnlyMatchingMods()
        {
            // Arrange
            var filterService = new ModFilterService();
            var mod1 = new GameMod(
                Id: "mod1",
                Level: 15,
                Pips: 6,
                Tier: 5,
                Slot: ModSlot.Square,
                Set: ModSet.Speed,
                Primary: new ModStat(StatType.Offense, 8.5),
                Secondaries: new List<ModStat> { new ModStat(StatType.Speed, 15) },
                EquippedUnitId: "CHAR1"
            );
            var mod2 = new GameMod(
                Id: "mod2",
                Level: 12,
                Pips: 5,
                Tier: 1,
                Slot: ModSlot.Arrow,
                Set: ModSet.Health,
                Primary: new ModStat(StatType.Speed, 30),
                Secondaries: new List<ModStat> { new ModStat(StatType.Defense, 10) },
                EquippedUnitId: null
            );

            var mods = new List<GameMod> { mod1, mod2 };
            var criteria = new ModFilterCriteria(
                IsEquipped: true,
                Slots: new List<ModSlot> { ModSlot.Square }
            );

            // Act
            var filtered = filterService.FilterMods(mods, criteria);

            // Assert
            Assert.Single(filtered);
            Assert.Equal("mod1", filtered[0].Id);
        }

        [Fact]
        public void FilterMods_WithSecondaryStatMinThreshold_ReturnsCorrectSubsets()
        {
            // Arrange
            var filterService = new ModFilterService();
            var mod1 = new GameMod(
                Id: "mod1",
                Level: 15,
                Pips: 5,
                Tier: 5,
                Slot: ModSlot.Circle,
                Set: ModSet.CritDamage,
                Primary: new ModStat(StatType.Protection, 23.5),
                Secondaries: new List<ModStat> { new ModStat(StatType.Speed, 20) },
                EquippedUnitId: null
            );
            var mod2 = new GameMod(
                Id: "mod2",
                Level: 15,
                Pips: 5,
                Tier: 5,
                Slot: ModSlot.Circle,
                Set: ModSet.CritDamage,
                Primary: new ModStat(StatType.Protection, 23.5),
                Secondaries: new List<ModStat> { new ModStat(StatType.Speed, 5) },
                EquippedUnitId: null
            );

            var mods = new List<GameMod> { mod1, mod2 };
            var criteria = new ModFilterCriteria(
                Secondaries: new List<SecondaryStatCriteria>
                {
                    new SecondaryStatCriteria(StatType.Speed, MinValue: 10.0)
                }
            );

            // Act
            var filtered = filterService.FilterMods(mods, criteria);

            // Assert
            Assert.Single(filtered);
            Assert.Equal("mod1", filtered[0].Id);
        }
    }
}