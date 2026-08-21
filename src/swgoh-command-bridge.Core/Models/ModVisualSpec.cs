using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace swgoh_command_bridge.Core.Models;

public sealed class ModVisualSpec
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("coordinate_system")]
    public ModVisualCoordinateSystem CoordinateSystem { get; set; } = new();

    [JsonPropertyName("asset_roots")]
    public ModVisualAssetRoots AssetRoots { get; set; } = new();

    [JsonPropertyName("set_icons")]
    public Dictionary<string, string> SetIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("shapes")]
    public Dictionary<string, ModVisualShape> Shapes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetShape(string name, out ModVisualShape shape) =>
        Shapes.TryGetValue(name, out shape!);

    public bool TryGetSetIcon(string name, out string assetName) =>
        SetIcons.TryGetValue(name, out assetName!);

    public IEnumerable<string> GetRequiredRuntimeAssets()
    {
        foreach (var setIcon in SetIcons.Values)
        {
            yield return CombineAssetPath(AssetRoots.SetIcons, setIcon);
        }

        foreach (var shape in Shapes.Values)
        {
            yield return CombineAssetPath(AssetRoots.Chassis, shape.Base.Sprite);
            if (shape.Light != null)
            {
                yield return CombineAssetPath(AssetRoots.Chassis, shape.Light.Sprite);
            }

            yield return CombineAssetPath(AssetRoots.Chassis, shape.SixDotSprite);
            yield return CombineAssetPath(AssetRoots.Chassis, shape.Level.Sprite);

            if (!string.IsNullOrWhiteSpace(shape.Level.SpritePattern))
            {
                for (var level = 1; level <= 15; level++)
                {
                    yield return CombineAssetPath(
                        AssetRoots.Chassis,
                        shape.Level.SpritePattern.Replace("{level}", level.ToString(), StringComparison.Ordinal));
                }
            }

            foreach (var pip in shape.RarityPips)
            {
                yield return CombineAssetPath(AssetRoots.Chassis, pip.Sprite);
            }
        }
    }

    private static string CombineAssetPath(string root, string fileName) =>
        string.IsNullOrWhiteSpace(root) ? fileName : $"{root.TrimEnd('/', '\\')}/{fileName}";
}

public sealed class ModVisualCoordinateSystem
{
    [JsonPropertyName("origin")]
    public string Origin { get; set; } = string.Empty;

    [JsonPropertyName("x_positive")]
    public string XPositive { get; set; } = string.Empty;

    [JsonPropertyName("y_positive")]
    public string YPositive { get; set; } = string.Empty;

    [JsonPropertyName("positions_are")]
    public string PositionsAre { get; set; } = string.Empty;

    [JsonPropertyName("screen_space_conversion")]
    public ModVisualScreenSpaceConversion ScreenSpaceConversion { get; set; } = new();

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;
}

public sealed class ModVisualScreenSpaceConversion
{
    [JsonPropertyName("x")]
    public string X { get; set; } = string.Empty;

    [JsonPropertyName("y")]
    public string Y { get; set; } = string.Empty;
}

public sealed class ModVisualAssetRoots
{
    [JsonPropertyName("chassis")]
    public string Chassis { get; set; } = string.Empty;

    [JsonPropertyName("set_icons")]
    public string SetIcons { get; set; } = string.Empty;
}

public sealed class ModVisualShape
{
    [JsonPropertyName("base")]
    public ModVisualObject Base { get; set; } = new();

    [JsonPropertyName("light")]
    public ModVisualObject? Light { get; set; }

    [JsonPropertyName("set_icon")]
    public ModVisualObject SetIcon { get; set; } = new();

    [JsonPropertyName("level")]
    public ModVisualObject Level { get; set; } = new();

    [JsonPropertyName("rarity_pips")]
    public List<ModVisualObject> RarityPips { get; set; } = new();

    [JsonPropertyName("six_dot_sprite")]
    public string SixDotSprite { get; set; } = string.Empty;
}

public sealed class ModVisualObject
{
    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("pivot")]
    public int Pivot { get; set; }

    [JsonPropertyName("color")]
    public ModVisualColor Color { get; set; } = new();

    [JsonPropertyName("local_scale")]
    public ModVisualVector3 LocalScale { get; set; } = new() { X = 1, Y = 1, Z = 1 };

    [JsonPropertyName("position")]
    public ModVisualVector3 Position { get; set; } = new();

    [JsonPropertyName("sprite_pattern")]
    public string? SpritePattern { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }
}

public sealed class ModVisualVector3
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }
}

public sealed class ModVisualColor
{
    [JsonPropertyName("r")]
    public double R { get; set; } = 1;

    [JsonPropertyName("g")]
    public double G { get; set; } = 1;

    [JsonPropertyName("b")]
    public double B { get; set; } = 1;

    [JsonPropertyName("a")]
    public double A { get; set; } = 1;
}

public static class ModVisualSpecJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ModVisualSpec Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("The mod visual spec is empty.");
        }

        var spec = JsonSerializer.Deserialize<ModVisualSpec>(json, Options)
            ?? throw new JsonException("The mod visual spec did not contain a JSON object.");

        if (spec.Version <= 0 || spec.Shapes.Count == 0 || spec.SetIcons.Count == 0)
        {
            throw new JsonException("The mod visual spec is missing its version, shapes, or set icon mapping.");
        }

        return spec;
    }
}

public static class ModVisualCoordinateConverter
{
    public static (double X, double Y) ToScreenPosition(ModVisualVector3 rootRelativePosition, double originX, double originY)
    {
        ArgumentNullException.ThrowIfNull(rootRelativePosition);
        return (originX + rootRelativePosition.X, originY - rootRelativePosition.Y);
    }
}
