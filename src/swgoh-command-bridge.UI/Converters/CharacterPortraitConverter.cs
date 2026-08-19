#nullable enable

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
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
