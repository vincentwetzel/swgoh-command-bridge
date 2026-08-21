#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.UI.Services;

public sealed class ModVisualAssetService
{
    public const string SpecResource = "avares://swgoh-command-bridge.UI/Assets/Mods/mod_visual_spec.json";
    private const string ChassisPrefix = "avares://swgoh-command-bridge.UI/Assets/Mods/Chassis/";
    private const string SetIconPrefix = "avares://swgoh-command-bridge.UI/Assets/Mods/SetIcons/";

    private readonly ConcurrentDictionary<string, Bitmap> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Bitmap> _tintedBitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<ModVisualSpec> _spec = new(LoadSpec);

    public ModVisualSpec Spec => _spec.Value;

    public Bitmap? GetChassisBitmap(string fileName) => GetBitmap(ChassisPrefix, fileName);

    public Bitmap? GetSetIconBitmap(string fileName) => GetBitmap(SetIconPrefix, fileName);

    public Bitmap? GetTintedBitmap(bool isSetIcon, string fileName, Color tint)
    {
        var prefix = isSetIcon ? SetIconPrefix : ChassisPrefix;
        var source = GetBitmap(prefix, fileName);
        if (source == null)
        {
            return null;
        }

        var cacheKey = $"{prefix}{fileName}|{tint.ToUInt32()}";
        return _tintedBitmapCache.GetOrAdd(cacheKey, _ => CreateTintedBitmap(source, tint));
    }

    public IReadOnlyList<string> ValidateRuntimeAssets()
    {
        var missing = new List<string>();
        foreach (var relativePath in Spec.GetRequiredRuntimeAssets().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var uri = ToResourceUri(relativePath);
            if (!AssetLoader.Exists(uri))
            {
                missing.Add(relativePath);
            }
        }

        return missing;
    }

    public string GetLevelAssetName(ModVisualShape shape, int level)
    {
        var pattern = shape.Level.SpritePattern;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return shape.Level.Sprite;
        }

        return pattern.Replace("{level}", level.ToString(), StringComparison.Ordinal);
    }

    public static string GetShapeName(ModSlot shape) =>
        shape.ToString().ToLowerInvariant();

    public static string GetSetName(ModSet set) => set switch
    {
        ModSet.Health => "health",
        ModSet.Offense => "offense",
        ModSet.Defense => "defense",
        ModSet.Speed => "speed",
        ModSet.CriticalChance => "critical_chance",
        ModSet.CriticalDamage => "critical_damage",
        ModSet.Potency => "potency",
        ModSet.Tenacity => "tenacity",
        _ => throw new ArgumentOutOfRangeException(nameof(set), set, "Unknown SWGOH mod set.")
    };

    private static ModVisualSpec LoadSpec()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(SpecResource, UriKind.Absolute));
            using var reader = new StreamReader(stream);
            return ModVisualSpecJson.Deserialize(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            throw new InvalidDataException("Unable to load the bundled SWGOH mod visual specification.", ex);
        }
    }

    private Bitmap? GetBitmap(string prefix, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var uri = new Uri(prefix + fileName, UriKind.Absolute);
        if (!AssetLoader.Exists(uri))
        {
            return null;
        }

        return _bitmapCache.GetOrAdd(prefix + fileName, _ =>
        {
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        });
    }

    private static Bitmap CreateTintedBitmap(Bitmap source, Color tint)
    {
        var pixelSize = source.PixelSize;
        var tinted = new WriteableBitmap(
            pixelSize,
            source.Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using var framebuffer = tinted.Lock();
        var byteCount = checked(framebuffer.RowBytes * pixelSize.Height);
        var pixels = new byte[byteCount];
        source.CopyPixels(framebuffer, AlphaFormat.Unpremul);
        Marshal.Copy(framebuffer.Address, pixels, 0, byteCount);

        for (var y = 0; y < pixelSize.Height; y++)
        {
            var rowOffset = y * framebuffer.RowBytes;
            for (var x = 0; x < pixelSize.Width; x++)
            {
                var pixelOffset = rowOffset + (x * 4);
                pixels[pixelOffset] = ScaleChannel(pixels[pixelOffset], tint.B);
                pixels[pixelOffset + 1] = ScaleChannel(pixels[pixelOffset + 1], tint.G);
                pixels[pixelOffset + 2] = ScaleChannel(pixels[pixelOffset + 2], tint.R);
                pixels[pixelOffset + 3] = ScaleChannel(pixels[pixelOffset + 3], tint.A);
            }
        }

        Marshal.Copy(pixels, 0, framebuffer.Address, byteCount);
        return tinted;
    }

    private static byte ScaleChannel(byte source, byte tint) =>
        (byte)((source * tint + 127) / 255);

    private static Uri ToResourceUri(string relativePath)
    {
        var separator = relativePath.IndexOf('/');
        if (separator < 0)
        {
            return new Uri(ChassisPrefix + relativePath, UriKind.Absolute);
        }

        var root = relativePath[..separator];
        var fileName = relativePath[(separator + 1)..];
        return root.Equals("battleui_view_rgba_sprites", StringComparison.OrdinalIgnoreCase)
            ? new Uri(SetIconPrefix + fileName, UriKind.Absolute)
            : new Uri(ChassisPrefix + fileName, UriKind.Absolute);
    }
}
