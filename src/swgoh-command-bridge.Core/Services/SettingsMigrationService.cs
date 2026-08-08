#nullable enable

using System;
using System.Collections.Generic;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Applies backward-compatible migrations to settings that predate stable threshold identities.
/// </summary>
public static class SettingsMigrationService
{
    public static AppSettings MigrateLegacyThresholdStorage(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var source = settings.UpgradeThresholds ?? new List<ModUpgradeThresholdSetting>();
        var migrated = new List<ModUpgradeThresholdSetting>(source.Count);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < source.Count; index++)
        {
            var threshold = source[index];
            var id = threshold.Id?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !usedIds.Add(id))
            {
                id = CreateStableId(index, usedIds);
            }

            var name = string.IsNullOrWhiteSpace(threshold.Name)
                ? $"Threshold {index + 1}"
                : threshold.Name.Trim();
            var statName = string.IsNullOrWhiteSpace(threshold.StatName)
                ? "Speed"
                : threshold.StatName.Trim();

            migrated.Add(threshold with
            {
                Id = id,
                Name = name,
                StatName = statName
            });
        }

        var defaultId = !string.IsNullOrWhiteSpace(settings.DefaultUpgradeThresholdId) &&
                        migrated.Exists(threshold => string.Equals(
                            threshold.Id,
                            settings.DefaultUpgradeThresholdId.Trim(),
                            StringComparison.Ordinal))
            ? settings.DefaultUpgradeThresholdId.Trim()
            : migrated.Count > 0 ? migrated[0].Id : null;

        return settings with
        {
            UpgradeThresholds = migrated,
            DefaultUpgradeThresholdId = defaultId
        };
    }

    private static string CreateStableId(int index, ISet<string> usedIds)
    {
        var baseId = $"threshold-{index}";
        var candidate = baseId;
        var suffix = 2;
        while (!usedIds.Add(candidate))
        {
            candidate = $"{baseId}-{suffix++}";
        }

        return candidate;
    }
}
