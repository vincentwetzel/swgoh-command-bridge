#nullable enable

using System;
using System.IO;
using System.Linq;
using Avalonia.Media;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.UI.Controls;
using swgoh_command_bridge.UI.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class ModVisualSpecTests
{
    [Fact]
    public void Spec_DeserializesAllShapesAndSets()
    {
        var spec = LoadSpec();

        Assert.Equal(2, spec.Version);
        Assert.Equal(
            new[] { "arrow", "circle", "cross", "diamond", "square", "triangle" },
            spec.Shapes.Keys.OrderBy(name => name));
        Assert.Equal(8, spec.SetIcons.Count);
        Assert.Equal("icon_buff_accuracy.png", spec.SetIcons["potency"]);
    }

    [Theory]
    [InlineData(ModSet.Health, "health")]
    [InlineData(ModSet.Offense, "offense")]
    [InlineData(ModSet.Defense, "defense")]
    [InlineData(ModSet.Speed, "speed")]
    [InlineData(ModSet.CriticalChance, "critical_chance")]
    [InlineData(ModSet.CriticalDamage, "critical_damage")]
    [InlineData(ModSet.Potency, "potency")]
    [InlineData(ModSet.Tenacity, "tenacity")]
    public void SetMapping_UsesTheSpecKeyForEveryKnownSet(ModSet set, string expectedKey)
    {
        Assert.Equal(expectedKey, ModVisualAssetService.GetSetName(set));
    }

    [Fact]
    public void Spec_ReferencesOnlyExistingRuntimeAssets()
    {
        var assetRoot = FindAssetRoot();
        var spec = LoadSpec();
        var missing = spec.GetRequiredRuntimeAssets()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(relativePath => !File.Exists(Path.Combine(assetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void RenderPlan_UsesSixDotSpriteAndOnlyRequestedPips()
    {
        var spec = LoadSpec();
        var request = new ModVisualRequest(
            ModSlot.Cross,
            ModSet.Speed,
            Dots: 6,
            Level: 15);

        var plan = ModVisualLayout.Create(spec, request);

        Assert.Contains(plan.Layers, layer => layer.AssetName == "modchip_cross_6dot.png");
        Assert.Contains(plan.Layers, layer => layer.AssetName == "icon_buff_speed.png");
        Assert.DoesNotContain(plan.Layers, layer => layer.AssetName == "lvl_15.png");
        Assert.Equal(6, plan.Layers.Count(layer => layer.AssetName == "pip.png"));
        Assert.All(plan.Layers, layer => Assert.True(layer.Width > 0 && layer.Height > 0));
    }

    [Fact]
    public void RenderPlan_UsesSixDotSpriteWhenClampedDotsReachSix()
    {
        var spec = LoadSpec();
        var plan = ModVisualLayout.Create(
            spec,
            new ModVisualRequest(ModSlot.Triangle, ModSet.Health, Dots: 99, Level: 99));

        Assert.Contains(plan.Layers, layer => layer.AssetName == "modchip_triangle_6dot.png");
        Assert.DoesNotContain(plan.Layers, layer => layer.AssetName == "lvl_15.png");
        Assert.Equal(7, plan.Layers.Count(layer => layer.AssetName == "pip.png"));
    }

    [Fact]
    public void RenderPlan_TintsNormalBorderByModTier()
    {
        var spec = LoadSpec();
        var plan = ModVisualLayout.Create(
            spec,
            new ModVisualRequest(ModSlot.Square, ModSet.Health, Dots: 5, Level: 15, Tier: 4));

        var light = Assert.Single(plan.Layers.Where(layer => layer.AssetName == "modchip_square_light.png"));
        Assert.Equal(ModVisualTierPalette.GetColor(4), light.TierTint);
    }

    [Fact]
    public void RenderPlan_DoesNotTintSixDotChassis()
    {
        var spec = LoadSpec();
        var plan = ModVisualLayout.Create(
            spec,
            new ModVisualRequest(
                ModSlot.Square,
                ModSet.Health,
                Dots: 6,
                Level: 15,
                Tier: 1));

        var light = Assert.Single(plan.Layers.Where(layer => layer.AssetName == "modchip_square_light.png"));
        Assert.Null(light.TierTint);

        var setIcon = Assert.Single(plan.Layers.Where(layer => layer.IsSetIcon));
        Assert.Equal(ModVisualTierPalette.GetColor(1), setIcon.TierTint);
    }

    [Theory]
    [InlineData(1, "#CDFFFF")]
    [InlineData(2, "#99FF33")]
    [InlineData(3, "#1D99FF")]
    [InlineData(4, "#A35EFF")]
    [InlineData(5, "#FFCC33")]
    public void TierPalette_UsesGameQualityColors(int tier, string expectedHex)
    {
        Assert.Equal(Color.Parse(expectedHex), ModVisualTierPalette.GetColor(tier));
    }

    [Fact]
    public void CoordinateConverter_InvertsUnityYForScreenCoordinates()
    {
        var screen = ModVisualCoordinateConverter.ToScreenPosition(
            new ModVisualVector3 { X = 9, Y = 10.5, Z = 0 },
            originX: 100,
            originY: 200);

        Assert.Equal(109, screen.X);
        Assert.Equal(189.5, screen.Y);
    }

    [Fact]
    public void LevelAssetName_ResolvesEverySupportedLevel()
    {
        var spec = LoadSpec();
        var shape = spec.Shapes["cross"];

        for (var level = 1; level <= 15; level++)
        {
            Assert.Equal($"lvl_{level}.png", new ModVisualAssetService().GetLevelAssetName(shape, level));
        }
    }

    private static ModVisualSpec LoadSpec()
    {
        var path = Path.Combine(FindAssetRoot(), "SWGOH_AssetDump", "mod_visual_spec.json");
        return ModVisualSpecJson.Deserialize(File.ReadAllText(path));
    }

    private static string FindAssetRoot()
    {
        var candidates = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var candidate in candidates)
        {
            var current = new DirectoryInfo(candidate);
            for (var depth = 0; current != null && depth < 10; depth++, current = current.Parent)
            {
                var direct = Path.Combine(current.FullName, "swgoh-command-bridge-assets");
                var sibling = Path.Combine(current.FullName, "..", "swgoh-command-bridge-assets");
                if (Directory.Exists(direct))
                {
                    return direct;
                }

                if (Directory.Exists(sibling))
                {
                    return Path.GetFullPath(sibling);
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the swgoh-command-bridge-assets repository.");
    }
}
