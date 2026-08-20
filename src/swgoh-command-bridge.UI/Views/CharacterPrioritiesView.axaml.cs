#nullable enable

using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.UI.Converters;
using swgoh_command_bridge.UI.ViewModels;

namespace swgoh_command_bridge.UI.Views;

/// <summary>
/// Hosts the priority board and its framework-level drag-and-drop interaction.
/// </summary>
public partial class CharacterPrioritiesView : UserControl
{
    private const string PriorityCardFormat = "swgoh-command-bridge/priority-card";
    private readonly CharacterPortraitConverter _portraitConverter = new();
    private readonly CharacterInitialsConverter _initialsConverter = new();
    private readonly CharacterDisplayNameConverter _displayNameConverter = new();
    private Border? _dragPreview;
    private Vector _dragPreviewPointerOffset;

    /// <summary>Initializes the priority board view.</summary>
    public CharacterPrioritiesView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, Tier_DragOver);
        AddHandler(DragDrop.DropEvent, Tier_Drop);
    }

    private async void CharacterCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control card ||
            card.DataContext is not CharacterEntity character ||
            !e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var data = new DataObject();
        data.Set(PriorityCardFormat, character);
        var originalOpacity = card.Opacity;
        card.Opacity = 0.35;
        _dragPreview = CreateDragPreview(character);
        DragPreviewLayer.Children.Add(_dragPreview);
        var pointerPosition = e.GetPosition(DragPreviewLayer);
        var cardPosition = card.TranslatePoint(default, DragPreviewLayer) ?? pointerPosition;
        _dragPreviewPointerOffset = pointerPosition - cardPosition;
        MoveDragPreview(pointerPosition);

        try
        {
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
        finally
        {
            card.Opacity = originalOpacity;
            DragPreviewLayer.Children.Remove(_dragPreview);
            _dragPreview = null;
            _dragPreviewPointerOffset = default;
        }

        e.Handled = true;
    }

    private void Tier_DragOver(object? sender, DragEventArgs e)
    {
        MoveDragPreview(e.GetPosition(DragPreviewLayer));
        e.DragEffects = FindTierDestination(e.Source) != null && e.Data.Contains(PriorityCardFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Tier_Drop(object? sender, DragEventArgs e)
    {
        if (FindTierDestination(e.Source) is not PriorityTierViewModel destination ||
            e.Data.Get(PriorityCardFormat) is not CharacterEntity character ||
            DataContext is not CharacterPrioritiesViewModel viewModel)
        {
            return;
        }

        var targetCard = FindPriorityCard(e.Source);
        var targetCharacter = targetCard?.DataContext as CharacterEntity;
        if (ReferenceEquals(targetCharacter, character))
        {
            return;
        }

        var destinationIndex = targetCharacter is null
            ? destination.Characters.Count
            : destination.Characters.IndexOf(targetCharacter);
        await viewModel.MoveCharacterAsync(character, destination, destinationIndex);
        e.Handled = true;
    }

    private static Control? FindPriorityCard(object? source)
    {
        var control = source as Control;
        while (control != null)
        {
            if (control.Classes.Contains("priority-card"))
            {
                return control;
            }

            control = control.Parent as Control;
        }

        return null;
    }

    private static PriorityTierViewModel? FindTierDestination(object? source)
    {
        var control = source as Control;
        while (control != null)
        {
            if (control.DataContext is PriorityTierViewModel destination &&
                DragDrop.GetAllowDrop(control))
            {
                return destination;
            }

            control = control.Parent as Control;
        }

        return null;
    }

    private Border CreateDragPreview(CharacterEntity character)
    {
        var portrait = _portraitConverter.Convert(
            character,
            typeof(IImage),
            null,
            CultureInfo.CurrentUICulture) as IImage;
        var initials = _initialsConverter.Convert(
            character,
            typeof(string),
            null,
            CultureInfo.CurrentUICulture)?.ToString() ?? "?";
        var name = _displayNameConverter.Convert(
            character,
            typeof(string),
            null,
            CultureInfo.CurrentUICulture)?.ToString() ?? string.Empty;
        var portraitGrid = new Grid();
        portraitGrid.Children.Add(new TextBlock
        {
            Text = initials,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.LightBlue
        });
        portraitGrid.Children.Add(new Image
        {
            Source = portrait,
            Stretch = Stretch.UniformToFill
        });

        return new Border
        {
            Width = 104,
            Padding = new Thickness(5),
            Opacity = 0.92,
            Background = new SolidColorBrush(Color.Parse("#304C6E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#A9DCFF")),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(7),
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new Border
                    {
                        Width = 70,
                        Height = 70,
                        CornerRadius = new CornerRadius(35),
                        ClipToBounds = true,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Child = portraitGrid
                    },
                    new TextBlock
                    {
                        Text = name,
                        FontSize = 12,
                        FontWeight = FontWeight.SemiBold,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        MaxHeight = 34,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            }
        };
    }

    private void MoveDragPreview(Point position)
    {
        if (_dragPreview == null)
        {
            return;
        }

        Canvas.SetLeft(_dragPreview, position.X - _dragPreviewPointerOffset.X);
        Canvas.SetTop(_dragPreview, position.Y - _dragPreviewPointerOffset.Y);
    }
}
