#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Serializes, migrates, and validates portable threshold documents independently of the UI.
/// </summary>
public sealed class ModThresholdTransferService
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string Serialize(IReadOnlyCollection<ModUpgradeThreshold> thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        var document = new ModThresholdTransferDocument(
            CurrentSchemaVersion,
            thresholds.Select(ToSetting).ToList());
        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    public IReadOnlyList<ModUpgradeThreshold> DeserializeAndValidate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("The threshold file is empty.");
        }

        var settings = ReadSettings(json);
        var thresholds = new List<ModUpgradeThreshold>(settings.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < settings.Count; index++)
        {
            var setting = settings[index];
            if (string.IsNullOrWhiteSpace(setting.Name))
            {
                throw new InvalidDataException($"Threshold {index + 1} is missing a name.");
            }

            if (setting.MinPips is < 1 or > 6)
            {
                throw new InvalidDataException($"Threshold '{setting.Name}' has invalid minimum pips.");
            }

            if (setting.MinTier is < 1 or > 5)
            {
                throw new InvalidDataException($"Threshold '{setting.Name}' has invalid minimum tier.");
            }

            if (!string.Equals(setting.StatName, "Speed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Threshold '{setting.Name}' uses unsupported stat '{setting.StatName}'. Only Speed is supported.");
            }

            if (double.IsNaN(setting.MinValue) ||
                double.IsInfinity(setting.MinValue) ||
                setting.MinValue < 0 ||
                setting.MinValue > int.MaxValue)
            {
                throw new InvalidDataException($"Threshold '{setting.Name}' has an invalid minimum speed.");
            }

            if (setting.UpgradeOnlyWithSpeed && setting.MinValue <= 0)
            {
                throw new InvalidDataException(
                    $"Threshold '{setting.Name}' requires a positive minimum speed when Require speed is enabled.");
            }

            if (double.IsNaN(setting.MinimumEfficiency) ||
                double.IsInfinity(setting.MinimumEfficiency) ||
                setting.MinimumEfficiency is < 0 or > 100)
            {
                throw new InvalidDataException($"Threshold '{setting.Name}' has invalid minimum efficiency.");
            }

            var id = string.IsNullOrWhiteSpace(setting.Id)
                ? Guid.NewGuid().ToString("N")
                : setting.Id.Trim();
            if (!ids.Add(id))
            {
                throw new InvalidDataException($"Threshold '{setting.Name}' has a duplicate ID.");
            }

            thresholds.Add(new ModUpgradeThreshold(
                id,
                setting.Name.Trim(),
                setting.MinPips,
                setting.MinTier,
                (int)Math.Round(setting.MinValue),
                setting.UpgradeOnlyWithSpeed,
                setting.MinimumEfficiency));
        }

        return thresholds.AsReadOnly();
    }

    private static List<ModUpgradeThresholdSetting> ReadSettings(string json)
    {
        try
        {
            var document = JsonSerializer.Deserialize<ModThresholdTransferDocument>(json, SerializerOptions);
            if (document != null)
            {
                if (document.SchemaVersion != CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported threshold transfer schema version {document.SchemaVersion}.");
                }

                return document.Thresholds ?? new List<ModUpgradeThresholdSetting>();
            }
        }
        catch (JsonException)
        {
            // Version 0 exports were plain arrays of threshold settings.
        }

        var legacySettings = JsonSerializer.Deserialize<List<ModUpgradeThresholdSetting>>(json, SerializerOptions);
        return legacySettings ?? throw new InvalidDataException("The threshold file is empty or invalid.");
    }

    private static ModUpgradeThresholdSetting ToSetting(ModUpgradeThreshold threshold) =>
        new(
            threshold.MinimumRarity,
            threshold.MinimumTier,
            "Speed",
            threshold.MinimumSpeed,
            threshold.Name,
            threshold.UpgradeOnlyWithSpeed,
            threshold.MinimumEfficiency,
            threshold.Id);
}
