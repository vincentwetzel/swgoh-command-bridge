#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI.Converters;

/// <summary>
/// Resolves a cached character entity to its exact bundled SWGOH portrait.
/// </summary>
public sealed class CharacterPortraitConverter : IValueConverter
{
    private const string ResourcePrefix = "avares://swgoh-command-bridge.UI/Assets/Portraits/";
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CharacterEntity character)
        {
            return null;
        }

        var portraitName = FindPortraitName(character);
        if (portraitName == null)
        {
            return null;
        }

        if (Cache.TryGetValue(portraitName, out var cachedPortrait))
        {
            return cachedPortrait;
        }

        var loadedPortrait = LoadPortrait(portraitName);
        return loadedPortrait == null ? null : Cache.GetOrAdd(portraitName, loadedPortrait);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static string? FindPortraitName(CharacterEntity character)
    {
        var portraitName = NormalizePortraitAsset(character.PortraitAsset);

        if (portraitName == null)
        {
            return null;
        }

        var uri = new Uri(ResourcePrefix + portraitName, UriKind.Absolute);
        return AssetLoader.Exists(uri) ? portraitName : null;
    }

    private static string? NormalizePortraitAsset(string? portraitAsset)
    {
        if (string.IsNullOrWhiteSpace(portraitAsset))
        {
            return null;
        }

        var fileName = portraitAsset.Trim();
        if (!fileName.StartsWith("charui_", StringComparison.OrdinalIgnoreCase))
        {
            fileName = $"charui_{fileName}";
        }

        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".png";
        }

        return fileName;
    }

    private static Bitmap? LoadPortrait(string resourceName)
    {
        try
        {
            var uri = new Uri(ResourcePrefix + resourceName, UriKind.Absolute);
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// Resolves the gear-tier border displayed over a character portrait.
/// </summary>
public sealed class CharacterTierHighlightConverter : IValueConverter
{
    private const string ResourcePrefix = "avares://swgoh-command-bridge.UI/Assets/TierHighlights/";
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<IReadOnlyDictionary<string, string>> BundledAlignments = new(LoadBundledAlignments);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CharacterEntity character)
        {
            return null;
        }

        var gearTier = Math.Clamp(character.GearLevel - 1, 0, 12);
        var resourceName = character.RelicTier > 0
            ? GetRelicHighlightName(ResolveAlignment(character))
            : $"TierHighlight{gearTier}.png";

        if (!AssetLoader.Exists(new Uri(ResourcePrefix + resourceName, UriKind.Absolute)))
        {
            return null;
        }

        if (Cache.TryGetValue(resourceName, out var cachedHighlight))
        {
            return cachedHighlight;
        }

        try
        {
            var uri = new Uri(ResourcePrefix + resourceName, UriKind.Absolute);
            using var stream = AssetLoader.Open(uri);
            var highlight = new Bitmap(stream);
            return Cache.GetOrAdd(resourceName, highlight);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static string GetRelicHighlightName(string? alignment) =>
        alignment?.Trim().ToLowerInvariant() switch
        {
            "dark side" or "dark" => "TierHighlight13_dark.png",
            "light side" or "light" => "TierHighlight13_light.png",
            _ => "TierHighlight13_neutral.png"
        };

    private static string ResolveAlignment(CharacterEntity character)
    {
        if (!string.IsNullOrWhiteSpace(character.Alignment) &&
            !string.Equals(character.Alignment, "Neutral", StringComparison.OrdinalIgnoreCase))
        {
            return character.Alignment;
        }

        return BundledAlignments.Value.TryGetValue(character.Id, out var bundledAlignment)
            ? bundledAlignment
            : character.Alignment;
    }

    private static IReadOnlyDictionary<string, string> LoadBundledAlignments()
    {
        var alignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var assembly = typeof(BundledCharacterCatalogService).Assembly;
            foreach (var resourceName in assembly.GetManifestResourceNames().Where(name =>
                         name.Contains(".Assets.CharacterCatalog.", StringComparison.OrdinalIgnoreCase) &&
                         name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    continue;
                }

                using var document = JsonDocument.Parse(stream);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var record in document.RootElement.EnumerateArray())
                {
                    if (!record.TryGetProperty("base_id", out var id) ||
                        !record.TryGetProperty("alignment", out var alignment) ||
                        id.ValueKind != JsonValueKind.String ||
                        alignment.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var normalized = alignment.GetString()?.Trim().ToLowerInvariant() switch
                    {
                        "dark side" or "dark" => "Dark Side",
                        "light side" or "light" => "Light Side",
                        _ => "Neutral"
                    };
                    alignments[id.GetString()!] = normalized;
                }
            }
        }
        catch (Exception)
        {
            // The neutral frame remains a safe fallback if bundled metadata cannot load.
        }

        return alignments;
    }
}

/// <summary>
/// Produces a compact fallback label when a character has no matching portrait.
/// </summary>
public sealed class CharacterInitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value is CharacterEntity character
            ? CharacterNameFormatter.Format(character.Id, character.Name)
            : value?.ToString();
        var words = text?
            .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 0)
            .ToArray();

        if (words is { Length: 2 } &&
            words[0].All(char.IsLetter) &&
            words[1].All(char.IsDigit))
        {
            return words[0].ToUpperInvariant();
        }

        return words is { Length: > 1 }
            ? string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])))
            : words is { Length: 1 } ? words[0][..1].ToUpperInvariant() : "?";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Displays the canonical character name when available and formats legacy raw IDs otherwise.
/// </summary>
public sealed class CharacterDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CharacterEntity character)
        {
            return CharacterNameFormatter.Format(character.Id, character.Name);
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
