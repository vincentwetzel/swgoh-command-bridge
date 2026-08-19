#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class PreferredModsAggregatorTests
{
    [Fact]
    public void Aggregate_ClassifiesClosePrimarySplitAsPreferredAndViableAlternative()
    {
        var observations = Enumerable.Range(0, 20)
            .Select(index => new PreferredModsObservation(CreateProfile(
                $"{index:D9}",
                index < 11 ? StatType.HealthPercent : StatType.ProtectionPercent,
                10 + index)))
            .ToList();
        var source = new PreferredModsSource("GAC", new[] { "Kyber Division 1" }, 20, 20);

        var result = new PreferredModsAggregator().Aggregate(
            observations,
            "test-1",
            source,
            new PreferredModsAggregationOptions(
                MinimumSampleSize: 10,
                MediumConfidenceSampleSize: 15,
                HighConfidenceSampleSize: 20,
                ViableAlternativeGap: 0.20),
            new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero));

        var character = Assert.Single(result.Characters);
        Assert.Equal(PreferredConfidence.High, character.Confidence);
        var triangle = Assert.Single(character.Slots.Where(slot => slot.Slot == ModSlot.Triangle));
        Assert.Collection(
            triangle.Options,
            option =>
            {
                Assert.Equal(StatType.HealthPercent, option.PrimaryStat);
                Assert.Equal(0.55, option.Share, 3);
                Assert.Equal(PreferredRecommendationStatus.Preferred, option.Status);
            },
            option =>
            {
                Assert.Equal(StatType.ProtectionPercent, option.PrimaryStat);
                Assert.Equal(0.45, option.Share, 3);
                Assert.Equal(PreferredRecommendationStatus.ViableAlternative, option.Status);
            });
        var setup = Assert.Single(character.Setups);
        Assert.Equal(StatType.HealthPercent, setup.SlotPrimaries.Single(primary => primary.Slot == ModSlot.Triangle).PrimaryStat);
        Assert.Contains(character.QualityProfiles, profile =>
            profile.Set == ModSet.CriticalDamage &&
            profile.Slot == ModSlot.Triangle &&
            profile.PrimaryStat == StatType.HealthPercent &&
            profile.SampleSize == 11);
    }

    [Fact]
    public void Aggregate_LimitsPublishedSetupPatterns()
    {
        var observations = Enumerable.Range(0, 20)
            .Select(index => new PreferredModsObservation(CreateProfile(
                $"{index:D9}",
                index < 11 ? StatType.HealthPercent : StatType.ProtectionPercent,
                10 + index)))
            .ToList();
        var result = new PreferredModsAggregator().Aggregate(
            observations,
            "test-setup-limit",
            new PreferredModsSource("GAC", new[] { "Kyber Division 1" }, 20, 20),
            new PreferredModsAggregationOptions(
                MinimumSampleSize: 10,
                MediumConfidenceSampleSize: 15,
                HighConfidenceSampleSize: 20,
                MaxSetupPatterns: 1));

        Assert.Single(Assert.Single(result.Characters).Setups);
    }

    private static PlayerProfile CreateProfile(string allyCode, StatType trianglePrimary, int speed)
    {
        var mods = new Dictionary<ModSlot, GameMod>();
        foreach (var slot in Enum.GetValues<ModSlot>())
        {
            var primary = slot == ModSlot.Triangle ? trianglePrimary : StatType.HealthPercent;
            mods[slot] = new GameMod(
                $"{allyCode}-{slot}",
                15,
                6,
                5,
                slot,
                slot == ModSlot.Triangle ? ModSet.CriticalDamage : ModSet.Health,
                new ModStat(primary, 1),
                new List<ModStat> { new(StatType.Speed, speed) },
                "TEST");
        }

        var character = new Character("TEST", "Test", 85, 13, 9, 1, 0, mods);
        return new PlayerProfile(allyCode, "Top Player", 85, 1, new[] { character }, mods.Values.ToList());
    }
}
