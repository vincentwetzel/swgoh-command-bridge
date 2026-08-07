#nullable enable

using System.Collections.Generic;
using System;

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
        List<ModUpgradeThresholdSetting>? UpgradeThresholds = null,
        string? DefaultUpgradeThresholdId = null,
        RecommendationScrapeSummary? LastRecommendationScrape = null,
        bool EnableLocalRecommendationScraping = true
    );

    /// <summary>
    /// Represents a user-defined threshold configuration for when a mod is considered worth upgrading.
    /// </summary>
    public record ModUpgradeThresholdSetting(
        int MinPips,
        int MinTier,
        string StatName,
        double MinValue,
        string Name = "Threshold",
        bool UpgradeOnlyWithSpeed = true,
        double MinimumEfficiency = 0,
        string Id = ""
    );

    /// <summary>
    /// Summarizes the most recent community recommendation refresh.
    /// </summary>
    public record RecommendationScrapeSummary(
        DateTime CompletedAtUtc,
        int Processed,
        int Succeeded,
        int Failed,
        bool Cancelled = false
    );
}
