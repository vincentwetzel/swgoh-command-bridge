#nullable enable

using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private readonly CharacterPortraitConverter _portraitConverter = new();
    private readonly CharacterInitialsConverter _initialsConverter = new();
    private readonly CharacterDisplayNameConverter _displayNameConverter = new();
    private Border? _dragPreview;
    private Vector _dragPreviewPointerOffset;
    private CharacterEntity? _draggedCharacter;
    private Control? _draggedCard;
    private IPointer? _dragPointer;
    private double _draggedCardOpacity;

    /// <summary>Initializes the priority board view.</summary>
    public CharacterPrioritiesView()
    {
        InitializeComponent();
        BoardRoot.AddHandler(
            InputElement.PointerMovedEvent,
            PriorityBoard_PointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        BoardRoot.AddHandler(
            InputElement.PointerReleasedEvent,
            PriorityBoard_PointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        BoardRoot.AddHandler(
            InputElement.PointerCaptureLostEvent,
            PriorityBoard_PointerCaptureLost,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        BoardRoot.AddHandler(
            InputElement.PointerWheelChangedEvent,
            PriorityBoard_PointerWheelChanged,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void CharacterCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control card ||
            card.DataContext is not CharacterEntity character ||
            !e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggedCardOpacity = card.Opacity;
        card.Opacity = 0.35;
        _draggedCharacter = character;
        _draggedCard = card;
        _dragPreview = CreateDragPreview(character);
        DragPreviewLayer.Children.Add(_dragPreview);
        var pointerPosition = e.GetPosition(DragPreviewLayer);
        var cardPosition = card.TranslatePoint(default, DragPreviewLayer) ?? pointerPosition;
        _dragPreviewPointerOffset = pointerPosition - cardPosition;
        MoveDragPreview(pointerPosition);

        _dragPointer = e.Pointer;
        e.Pointer.Capture(card);
        e.Handled = true;
    }

    private void PriorityBoard_PointerMoved(object? sender, PointerEventArgs e)
    {
        MoveDragPreview(e.GetPosition(DragPreviewLayer));
    }

    private void PriorityBoard_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_dragPreview == null)
        {
            return;
        }

        var offset = PriorityBoardScrollViewer.Offset;
        var maximumOffset = Math.Max(0, PriorityBoardScrollViewer.Extent.Height - PriorityBoardScrollViewer.Viewport.Height);
        var verticalOffset = Math.Clamp(offset.Y - e.Delta.Y * 48, 0, maximumOffset);
        PriorityBoardScrollViewer.Offset = new Vector(offset.X, verticalOffset);
        e.Handled = true;
    }

    private async void PriorityBoard_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedCharacter is not CharacterEntity character ||
            DataContext is not CharacterPrioritiesViewModel viewModel)
        {
            return;
        }

        var target = BoardRoot.InputHitTest(e.GetPosition(BoardRoot));
        var destination = FindTierDestination(target);
        var targetCard = FindPriorityCard(target);
        var targetCharacter = targetCard?.DataContext as CharacterEntity;
        EndDrag(e.Pointer);
        if (destination is null || ReferenceEquals(targetCharacter, character))
        {
            return;
        }

        var destinationIndex = targetCharacter is null
            ? destination.Characters.Count
            : destination.Characters.IndexOf(targetCharacter);
        await viewModel.MoveCharacterAsync(character, destination, destinationIndex);
        e.Handled = true;
    }

    private void PriorityBoard_PointerCaptureLost(object? sender, PointerEventArgs e)
    {
        EndDrag(null);
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
                control is Border)
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

    private void EndDrag(IPointer? pointer)
    {
        if (_dragPreview == null)
        {
            return;
        }

        var preview = _dragPreview;
        var draggedCard = _draggedCard;
        _dragPreview = null;
        _dragPreviewPointerOffset = default;
        _draggedCharacter = null;
        _draggedCard = null;
        _dragPointer = null;
        if (draggedCard != null)
        {
            draggedCard.Opacity = _draggedCardOpacity;
        }
        _draggedCardOpacity = 1;
        pointer?.Capture(null);
        DragPreviewLayer.Children.Remove(preview);
    }
}
