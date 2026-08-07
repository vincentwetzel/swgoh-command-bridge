#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Serializes and validates portable application settings without exporting embedded URL credentials.
/// </summary>
public sealed class SettingsTransferService
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string Serialize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var exportSettings = settings;
        if (Uri.TryCreate(settings.ComlinkBaseUrl?.Trim(), UriKind.Absolute, out var url) &&
            !string.IsNullOrWhiteSpace(url.UserInfo))
        {
            var safeUrl = new UriBuilder(url)
            {
                UserName = string.Empty,
                Password = string.Empty
            };
            exportSettings = settings with { ComlinkBaseUrl = safeUrl.Uri.ToString() };
        }

        var safeSettings = NormalizeAndValidate(exportSettings, allowMissingAllyCode: true);
        return JsonSerializer.Serialize(
            new SettingsTransferDocument(CurrentSchemaVersion, safeSettings),
            SerializerOptions);
    }

    public AppSettings DeserializeAndValidate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("The settings file is empty.");
        }

        SettingsTransferDocument? document = null;
        try
        {
            document = JsonSerializer.Deserialize<SettingsTransferDocument>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            // Version 0 files were plain AppSettings objects.
        }

        if (document != null && document.Settings != null)
        {
            if (document.SchemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported settings transfer schema version {document.SchemaVersion}.");
            }

            return NormalizeAndValidate(document.Settings, allowMissingAllyCode: true);
        }

        try
        {
            var legacySettings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return legacySettings == null
                ? throw new InvalidDataException("The settings file is invalid.")
                : NormalizeAndValidate(legacySettings, allowMissingAllyCode: true);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The settings file is invalid.", ex);
        }
    }

    private static AppSettings NormalizeAndValidate(AppSettings settings, bool allowMissingAllyCode)
    {
        if (!Uri.TryCreate(settings.ComlinkBaseUrl?.Trim(), UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrWhiteSpace(url.UserInfo))
        {
            throw new InvalidDataException(
                "Settings must contain an absolute HTTP or HTTPS Comlink URL without embedded credentials.");
        }

        string? allyCode = null;
        if (!string.IsNullOrWhiteSpace(settings.DefaultAllyCode))
        {
            if (!AllyCodeValidator.TryNormalize(settings.DefaultAllyCode, out allyCode, out var errorMessage))
            {
                throw new InvalidDataException(errorMessage);
            }
        }
        else if (!allowMissingAllyCode)
        {
            throw new InvalidDataException("Settings must contain a default ally code.");
        }

        var theme = string.IsNullOrWhiteSpace(settings.Theme) ? "Dark" : settings.Theme.Trim();
        if (theme.Length > 64)
        {
            throw new InvalidDataException("The settings theme name is too long.");
        }

        var thresholds = settings.UpgradeThresholds ?? new List<ModUpgradeThresholdSetting>();
        ValidateThresholds(thresholds);

        return settings with
        {
            ComlinkBaseUrl = url.ToString().TrimEnd('/') + "/",
            DefaultAllyCode = allyCode,
            Theme = theme,
            UpgradeThresholds = thresholds
        };
    }

    private static void ValidateThresholds(IReadOnlyList<ModUpgradeThresholdSetting> thresholds)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < thresholds.Count; index++)
        {
            var threshold = thresholds[index];
            if (string.IsNullOrWhiteSpace(threshold.Name))
            {
                throw new InvalidDataException($"Threshold {index + 1} is missing a name.");
            }

            if (threshold.MinPips is < 1 or > 6)
            {
                throw new InvalidDataException($"Threshold '{threshold.Name}' has invalid minimum pips.");
            }

            if (threshold.MinTier is < 1 or > 5)
            {
                throw new InvalidDataException($"Threshold '{threshold.Name}' has invalid minimum tier.");
            }

            if (!string.Equals(threshold.StatName, "Speed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Threshold '{threshold.Name}' uses unsupported stat '{threshold.StatName}'. Only Speed is supported.");
            }

            if (double.IsNaN(threshold.MinValue) ||
                double.IsInfinity(threshold.MinValue) ||
                threshold.MinValue < 0 ||
                threshold.MinValue > int.MaxValue)
            {
                throw new InvalidDataException($"Threshold '{threshold.Name}' has an invalid minimum speed.");
            }

            if (threshold.UpgradeOnlyWithSpeed && threshold.MinValue <= 0)
            {
                throw new InvalidDataException(
                    $"Threshold '{threshold.Name}' requires a positive minimum speed when Require speed is enabled.");
            }

            if (double.IsNaN(threshold.MinimumEfficiency) ||
                double.IsInfinity(threshold.MinimumEfficiency) ||
                threshold.MinimumEfficiency is < 0 or > 100)
            {
                throw new InvalidDataException($"Threshold '{threshold.Name}' has invalid minimum efficiency.");
            }

            if (!string.IsNullOrWhiteSpace(threshold.Id) && !ids.Add(threshold.Id.Trim()))
            {
                throw new InvalidDataException($"Threshold '{threshold.Name}' has a duplicate ID.");
            }
        }
    }
}
