#nullable enable

using System.Collections.Generic;

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Holds application-wide settings and user configurations.
    /// </summary>
    public record AppSettings(
        string ComlinkBaseUrl = "http://localhost:3000",
        string? DefaultAllyCode = null,
        string Theme = "Dark",
        bool AutomaticallyCheckForUpdates = true,
        List<ModUpgradeThresholdSetting>? UpgradeThresholds = null
    );

    /// <summary>
    /// Represents a user-defined threshold configuration for when a mod is considered worth upgrading.
    /// </summary>
    public record ModUpgradeThresholdSetting(
        int MinPips,
        int MinTier,
        string StatName,
        double MinValue
    );
}