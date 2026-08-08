#nullable enable

using System.Linq;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class PersistedModelMapperTests
{
    [Fact]
    public void ToGameMod_PreservesPersistedStatsAndEquippedOwner()
    {
        var entity = new GameModEntity
        {
            Id = "persisted-mod",
            CharacterId = "REY",
            Level = 15,
            Rarity = 6,
            Tier = 5,
            Slot = (int)ModSlot.Arrow,
            Set = (int)ModSet.Speed,
            PrimaryStatType = nameof(StatType.Speed),
            PrimaryStatValue = 30,
            SecondaryStatsJson =
                "[{\"Type\":\"Speed\",\"Value\":15,\"RollCount\":3},{\"Type\":\"Potency\",\"Value\":5,\"RollCount\":1}]"
        };

        var model = PersistedModelMapper.ToGameMod(entity);

        Assert.Equal("persisted-mod", model.Id);
        Assert.Equal(15, model.Level);
        Assert.Equal(6, model.Pips);
        Assert.Equal(5, model.Tier);
        Assert.Equal(ModSlot.Arrow, model.Slot);
        Assert.Equal(ModSet.Speed, model.Set);
        Assert.Equal(StatType.Speed, model.Primary.Type);
        Assert.Equal(30d, model.Primary.Value);
        Assert.Equal("REY", model.EquippedUnitId);
        Assert.Equal(2, model.Secondaries.Count);
        Assert.Equal(3, model.Secondaries.Single(stat => stat.Type == StatType.Speed).RollCount);
    }

    [Fact]
    public void ToGameMod_IgnoresMalformedSecondaryEntriesWithoutDroppingValidStats()
    {
        var model = PersistedModelMapper.ToGameMod(new GameModEntity
        {
            Id = "tolerant-mod",
            PrimaryStatType = "UnknownStat",
            SecondaryStatsJson =
                "[{\"Type\":\"Speed\",\"Value\":10,\"RollCount\":2},{\"Type\":\"NotAStat\",\"Value\":99,\"RollCount\":1}]"
        });

        Assert.Equal(StatType.None, model.Primary.Type);
        var secondary = Assert.Single(model.Secondaries);
        Assert.Equal(StatType.Speed, secondary.Type);
        Assert.Equal(2, secondary.RollCount);
    }

    [Fact]
    public void ToCharacter_PreservesCharacterFieldsAndValidEquippedSlots()
    {
        var character = PersistedModelMapper.ToCharacter(
            new CharacterEntity
            {
                Id = "REY",
                Name = "Rey",
                Level = 85,
                GearLevel = 13,
                Stars = 7,
                GalacticPower = 25000,
                Priority = 42
            },
            new[]
            {
                new GameModEntity
                {
                    Id = "valid-equipped",
                    CharacterId = "REY",
                    Slot = (int)ModSlot.Square,
                    Set = (int)ModSet.Health,
                    PrimaryStatType = nameof(StatType.Health)
                },
                new GameModEntity { Id = "invalid-slot", Slot = 99 }
            });

        Assert.Equal("REY", character.Id);
        Assert.Equal("Rey", character.Name);
        Assert.Equal(85, character.Level);
        Assert.Equal(13, character.GearLevel);
        Assert.Equal(7, character.Stars);
        Assert.Equal(25000, character.GalacticPower);
        Assert.Equal(42, character.Priority);
        Assert.True(character.EquippedMods.ContainsKey(ModSlot.Square));
        Assert.Single(character.EquippedMods);
    }
}
