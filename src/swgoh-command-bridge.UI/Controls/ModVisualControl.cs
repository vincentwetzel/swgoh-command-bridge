#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.UI.Services;

namespace swgoh_command_bridge.UI.Controls;

public sealed class ModVisualControl : Canvas
{
    public static readonly StyledProperty<ModVisualRequest?> RequestProperty =
        AvaloniaProperty.Register<ModVisualControl, ModVisualRequest?>(nameof(Request));

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<ModVisualControl, double>(nameof(Scale), 1d);

    public static readonly StyledProperty<GameModEntity?> ModProperty =
        AvaloniaProperty.Register<ModVisualControl, GameModEntity?>(nameof(Mod));

    private readonly ModVisualAssetService _assetService;
    private ModVisualLayout? _layout;
    private string? _failureMessage;
    private bool _isBuilt;

    public ModVisualControl()
        : this(SharedAssetService.Instance)
    {
    }

    public ModVisualControl(ModVisualAssetService assetService)
    {
        _assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
        ClipToBounds = false;
    }

    public ModVisualRequest? Request
    {
        get => GetValue(RequestProperty);
        set => SetValue(RequestProperty, value);
    }

    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public GameModEntity? Mod
    {
        get => GetValue(ModProperty);
        set => SetValue(ModProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureBuilt();
        var scale = GetSafeScale();
        var desired = _layout == null
            ? string.IsNullOrWhiteSpace(_failureMessage) ? new Size(0, 0) : new Size(180, 40)
            : new Size(_layout.Width * scale, _layout.Height * scale);

        foreach (var child in Children)
        {
            child.Measure(desired);
        }

        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureBuilt();
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RequestProperty || change.Property == ScaleProperty || change.Property == ModProperty)
        {
            _isBuilt = false;
            InvalidateMeasure();
        }
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        Children.Clear();
        _layout = null;
        _failureMessage = null;
        _isBuilt = true;

        var request = Request ?? (Mod == null ? null : ModVisualRequest.FromEntity(Mod));
        if (request == null)
        {
            return;
        }

        try
        {
            _layout = ModVisualLayout.Create(_assetService.Spec, request);
            var scale = GetSafeScale();
            foreach (var layer in _layout.Layers.OrderBy(layer => layer.Depth))
            {
                var bitmap = layer.TierTint is Color tierColor
                    ? _assetService.GetTintedBitmap(layer.IsSetIcon, layer.AssetName, tierColor)
                    : layer.IsSetIcon
                        ? _assetService.GetSetIconBitmap(layer.AssetName)
                        : _assetService.GetChassisBitmap(layer.AssetName);
                if (bitmap == null)
                {
                    AddFailureText($"Missing mod art: {layer.AssetName}");
                    _layout = null;
                    return;
                }

                Control visual = new Image
                {
                    Source = bitmap,
                    Width = layer.Width * scale,
                    Height = layer.Height * scale,
                    Stretch = Stretch.Uniform,
                    Opacity = Math.Clamp(layer.Opacity, 0, 1),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(visual, layer.Left * scale);
                Canvas.SetTop(visual, layer.Top * scale);
                visual.SetValue(Panel.ZIndexProperty, layer.Depth);
                Children.Add(visual);
            }
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidDataException)
        {
            AddFailureText(ex.Message);
            _layout = null;
        }
    }

    private void AddFailureText(string message)
    {
        Children.Clear();
        _failureMessage = message;
        Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.OrangeRed,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 180,
            IsHitTestVisible = false
        });
    }

    private double GetSafeScale() => double.IsFinite(Scale) && Scale > 0 ? Scale : 1d;

    private sealed class SharedAssetService
    {
        public static readonly ModVisualAssetService Instance = new();
    }
}

public sealed class ModVisualLayout
{
    private ModVisualLayout(double width, double height, IReadOnlyList<ModVisualLayer> layers)
    {
        Width = width;
        Height = height;
        Layers = layers;
    }

    public double Width { get; }
    public double Height { get; }
    public IReadOnlyList<ModVisualLayer> Layers { get; }

    public static ModVisualLayout Create(ModVisualSpec spec, ModVisualRequest request)
    {
        var shapeName = ModVisualAssetService.GetShapeName(request.Shape);
        if (!spec.TryGetShape(shapeName, out var shape))
        {
            throw new KeyNotFoundException($"Unknown mod shape '{shapeName}'.");
        }

        var setName = ModVisualAssetService.GetSetName(request.Set);
        if (!spec.TryGetSetIcon(setName, out var setIcon))
        {
            throw new KeyNotFoundException($"Unknown mod set '{setName}'.");
        }

        var dots = Math.Clamp(request.Dots, 1, 7);
        var tier = Math.Clamp(request.Tier, 1, 5);
        var layers = new List<ModVisualLayer>();
        var isSixDot = dots >= 6;
        var chassisSprite = isSixDot
            ? shape.SixDotSprite
            : shape.Base.Sprite;
        var tierTint = isSixDot
            ? (Color?)null
            : ModVisualTierPalette.GetColor(tier);
        var setIconTint = ModVisualTierPalette.GetColor(tier);

        AddLayer(layers, shape.Base, chassisSprite, false);
        if (shape.Light != null)
        {
            AddLayer(layers, shape.Light, shape.Light.Sprite, false, tierTint);
        }

        // The game applies the mod quality color to the set emblem as well as
        // the chassis border. The extracted emblem PNGs are white masks.
        AddLayer(layers, shape.SetIcon, setIcon, true, setIconTint);

        foreach (var pip in shape.RarityPips.Where(pip => pip.Index is >= 1 and <= 7 && pip.Index <= dots))
        {
            AddLayer(layers, pip, pip.Sprite, false);
        }

        if (layers.Count == 0)
        {
            throw new InvalidDataException($"Mod shape '{shapeName}' did not define any visual layers.");
        }

        var minX = layers.Min(layer => layer.PositionX - layer.Width / 2);
        var maxX = layers.Max(layer => layer.PositionX + layer.Width / 2);
        var minY = layers.Min(layer => layer.ScreenPositionY - layer.Height / 2);
        var maxY = layers.Max(layer => layer.ScreenPositionY + layer.Height / 2);
        var padding = 1d;
        foreach (var layer in layers)
        {
            layer.Left = layer.PositionX - layer.Width / 2 - minX + padding;
            layer.Top = layer.ScreenPositionY - layer.Height / 2 - minY + padding;
        }

        return new ModVisualLayout(maxX - minX + padding * 2, maxY - minY + padding * 2, layers);
    }

    private static void AddLayer(
        List<ModVisualLayer> layers,
        ModVisualObject definition,
        string assetName,
        bool isSetIcon,
        Color? tierTint = null)
    {
        var scaleX = Math.Abs(definition.LocalScale.X) > 0 ? Math.Abs(definition.LocalScale.X) : 1;
        var scaleY = Math.Abs(definition.LocalScale.Y) > 0 ? Math.Abs(definition.LocalScale.Y) : 1;
        var screenPosition = ModVisualCoordinateConverter.ToScreenPosition(definition.Position, 0, 0);
        layers.Add(new ModVisualLayer(
            assetName,
            definition.Position.X,
            screenPosition.Y,
            Math.Max(1, definition.Width * scaleX),
            Math.Max(1, definition.Height * scaleY),
            definition.Depth,
            Math.Clamp(definition.Color.A, 0, 1),
            isSetIcon,
            tierTint));
    }
}

/// <summary>
/// The normal chassis PNGs are neutral artwork. In the game, the slot-border
/// layer is multiplied by the mod quality color at runtime.
/// </summary>
public static class ModVisualTierPalette
{
    // These are the in-game SWGOH mod-quality colors supplied from the asset
    // investigation. The set emblem and the quality border use the same tier.
    public static readonly Color Tier1Gray = Color.Parse("#CDFFFF");
    public static readonly Color Tier2Green = Color.Parse("#99FF33");
    public static readonly Color Tier3Blue = Color.Parse("#1D99FF");
    public static readonly Color Tier4Purple = Color.Parse("#A35EFF");
    public static readonly Color Tier5Gold = Color.Parse("#FFCC33");

    public static Color GetColor(int tier) => Math.Clamp(tier, 1, 5) switch
    {
        1 => Tier1Gray,
        2 => Tier2Green,
        3 => Tier3Blue,
        4 => Tier4Purple,
        _ => Tier5Gold
    };
}

public sealed class ModVisualLayer
{
    public ModVisualLayer(
        string assetName,
        double positionX,
        double positionY,
        double width,
        double height,
        int depth,
        double opacity,
        bool isSetIcon,
        Color? tierTint)
    {
        AssetName = assetName;
        PositionX = positionX;
        ScreenPositionY = positionY;
        Width = width;
        Height = height;
        Depth = depth;
        Opacity = opacity;
        IsSetIcon = isSetIcon;
        TierTint = tierTint;
    }

    public string AssetName { get; }
    public double PositionX { get; }
    public double ScreenPositionY { get; }
    public double Width { get; }
    public double Height { get; }
    public int Depth { get; }
    public double Opacity { get; }
    public bool IsSetIcon { get; }
    public Color? TierTint { get; }
    public double Left { get; set; }
    public double Top { get; set; }
}
