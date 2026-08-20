#nullable enable

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.UI.ViewModels;

/// <summary>
/// Represents one destination row on the roster priority board.
/// </summary>
public sealed class PriorityTierViewModel : ObservableObject
{
    /// <summary>Initializes a tier row.</summary>
    public PriorityTierViewModel(PriorityTier tier, string title, string accentColor)
    {
        Tier = tier;
        Title = title;
        AccentColor = accentColor;
        Characters.CollectionChanged += OnCharactersChanged;
    }

    /// <summary>Gets the persistence value for this tier.</summary>
    public PriorityTier Tier { get; }

    /// <summary>Gets the user-facing tier label.</summary>
    public string Title { get; }

    /// <summary>Gets the visual color used for the tier label.</summary>
    public string AccentColor { get; }

    /// <summary>Gets the units ordered from highest to lowest within this tier.</summary>
    public ObservableCollection<CharacterEntity> Characters { get; } = new();

    /// <summary>Gets the number of units in this tier.</summary>
    public int Count => Characters.Count;

    private void OnCharactersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Count));
    }
}
