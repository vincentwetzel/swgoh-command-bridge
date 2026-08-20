using swgoh_command_bridge.Core.Models;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ModPrimaryRulesTests
{
    [Theory]
    [InlineData(ModSlot.Square, StatType.OffensePercent)]
    [InlineData(ModSlot.Diamond, StatType.DefensePercent)]
    [InlineData(ModSlot.Arrow, StatType.Speed)]
    [InlineData(ModSlot.Triangle, StatType.CriticalDamage)]
    [InlineData(ModSlot.Circle, StatType.ProtectionPercent)]
    [InlineData(ModSlot.Cross, StatType.Tenacity)]
    public void IsAllowed_AcceptsLegalShapePrimaryPairs(ModSlot slot, StatType primary)
    {
        Assert.True(ModPrimaryRules.IsAllowed(slot, primary));
    }

    [Theory]
    [InlineData(ModSlot.Square, StatType.Accuracy, StatType.OffensePercent)]
    [InlineData(ModSlot.Diamond, StatType.CriticalAvoidance, StatType.DefensePercent)]
    [InlineData(ModSlot.Circle, StatType.CriticalDamage, StatType.None)]
    [InlineData(ModSlot.Cross, StatType.Accuracy, StatType.None)]
    public void Normalize_CorrectsFixedShapesAndRejectsOtherIllegalPairs(
        ModSlot slot,
        StatType sourcePrimary,
        StatType expectedPrimary)
    {
        Assert.Equal(expectedPrimary, ModPrimaryRules.Normalize(slot, sourcePrimary));
    }
}
