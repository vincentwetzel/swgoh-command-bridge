#nullable enable

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Domain model representing user-defined scoring and upgrade guidelines for mods.
    /// </summary>
    public record ModUpgradeThreshold(
        string Id,
        string Name,
        int MinimumRarity,
        int MinimumTier,
        int MinimumSpeed,
        bool UpgradeOnlyWithSpeed,
        double MinimumEfficiency
    );
}