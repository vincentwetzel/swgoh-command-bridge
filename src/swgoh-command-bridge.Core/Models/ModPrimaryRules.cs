namespace swgoh_command_bridge.Core.Models;

/// <summary>
/// Canonical SWGOH rules for which primary stats may appear on each mod shape.
/// These rules protect downstream recommendations from malformed or version-skewed
/// source payloads.
/// </summary>
public static class ModPrimaryRules
{
    public static bool IsAllowed(ModSlot slot, StatType primary) => slot switch
    {
        ModSlot.Square => primary == StatType.OffensePercent,
        ModSlot.Arrow => primary is StatType.Speed or StatType.HealthPercent or StatType.ProtectionPercent or
            StatType.OffensePercent or StatType.DefensePercent or StatType.Accuracy,
        ModSlot.Diamond => primary == StatType.DefensePercent,
        ModSlot.Triangle => primary is StatType.HealthPercent or StatType.ProtectionPercent or
            StatType.OffensePercent or StatType.DefensePercent or StatType.CriticalDamage or
            StatType.CriticalChance or StatType.CriticalChancePercent,
        ModSlot.Circle => primary is StatType.HealthPercent or StatType.ProtectionPercent,
        ModSlot.Cross => primary is StatType.HealthPercent or StatType.ProtectionPercent or
            StatType.OffensePercent or StatType.DefensePercent or StatType.Potency or
            StatType.Tenacity,
        _ => false
    };

    /// <summary>
    /// Recovers the two fixed primaries when a source payload exposes a legacy or
    /// inconsistent unit-stat identifier. Other invalid pairs are marked unknown
    /// so they cannot affect recommendations.
    /// </summary>
    public static StatType Normalize(ModSlot slot, StatType primary)
    {
        if (primary == StatType.None || !Enum.IsDefined(primary))
        {
            return StatType.None;
        }

        return slot switch
        {
            ModSlot.Square => StatType.OffensePercent,
            ModSlot.Diamond => StatType.DefensePercent,
            ModSlot.Triangle when primary == StatType.Accuracy => StatType.OffensePercent,
            ModSlot.Triangle when primary == StatType.CriticalAvoidance => StatType.DefensePercent,
            ModSlot.Cross when primary == StatType.Accuracy => StatType.OffensePercent,
            ModSlot.Cross when primary == StatType.CriticalAvoidance => StatType.DefensePercent,
            _ => IsAllowed(slot, primary) ? primary : StatType.None
        };
    }
}
