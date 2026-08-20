#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI.ViewModels;

/// <summary>
/// Represents the drag-and-drop tier-list board used to prioritize roster units.
/// </summary>
public sealed class CharacterPrioritiesViewModel : StateViewModelBase<IReadOnlyList<CharacterEntity>>
{
    private readonly AppDbContext _context;
    private readonly Func<string?>? _activeAllyCodeProvider;
    private readonly IRosterUnitClassifier _unitClassifier;
    private bool _showShips;

    /// <summary>Raised after a priority placement has been persisted successfully.</summary>
    public event Func<Task>? PrioritiesChanged;

    /// <summary>Gets all loaded units for the active account scope.</summary>
    public ObservableCollection<CharacterEntity> Characters { get; } = new();

    /// <summary>Gets the ranked tier rows in display order.</summary>
    public ObservableCollection<PriorityTierViewModel> RankedTiers { get; } = new(
    [
        new(PriorityTier.S, "S", "#E6C229"),
        new(PriorityTier.A, "A", "#4FC3F7"),
        new(PriorityTier.B, "B", "#66BB6A"),
        new(PriorityTier.C, "C", "#FFB74D"),
        new(PriorityTier.D, "D", "#EF5350")
    ]);

    /// <summary>Gets the holding area for units that are not a current priority.</summary>
    public PriorityTierViewModel UnrankedTier { get; } = new(
        PriorityTier.Unranked,
        "Unranked",
        "#78909C");

    /// <summary>Initializes the priority board.</summary>
    public CharacterPrioritiesViewModel(AppDbContext context)
        : this(context, null)
    {
    }

    /// <summary>Initializes the priority board for an active account scope.</summary>
    public CharacterPrioritiesViewModel(
        AppDbContext context,
        Func<string?>? activeAllyCodeProvider,
        IRosterUnitClassifier? unitClassifier = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _activeAllyCodeProvider = activeAllyCodeProvider;
        _unitClassifier = unitClassifier ?? new BundledRosterUnitClassifier();
        RefreshCommand = new AsyncRelayCommand(LoadCharactersAsync);
        ShowCharactersCommand = new RelayCommand(() => ShowShips = false);
        ShowShipsCommand = new RelayCommand(() => ShowShips = true);
    }

    /// <summary>Gets the heading shown above the board.</summary>
    public string HeaderText => ShowShips
        ? "Roster · Ship Priorities"
        : "Roster · Character Priorities";

    /// <summary>Gets whether the ship board is currently visible.</summary>
    public bool ShowShips
    {
        get => _showShips;
        set
        {
            if (_showShips == value)
            {
                return;
            }

            _showShips = value;
            OnPropertyChanged(nameof(ShowShips));
            OnPropertyChanged(nameof(IsCharacterBoard));
            OnPropertyChanged(nameof(HeaderText));
            RebuildBoard();
        }
    }

    /// <summary>Gets whether the character board is currently visible.</summary>
    public bool IsCharacterBoard => !ShowShips;

    /// <summary>Gets whether roster data has loaded successfully.</summary>
    public bool HasCharacters => State.Status == OperationStatus.Success;

    /// <summary>Gets whether the active board currently has no matching units.</summary>
    public bool IsActiveBoardEmpty => HasCharacters && !RankedTiers.Any(tier => tier.Count > 0) && UnrankedTier.Count == 0;

    /// <summary>Reloads the board from the local roster cache.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Shows character priorities.</summary>
    public IRelayCommand ShowCharactersCommand { get; }

    /// <summary>Shows ship priorities.</summary>
    public IRelayCommand ShowShipsCommand { get; }

    /// <summary>
    /// Loads all roster units and rebuilds the currently selected board.
    /// </summary>
    public async Task LoadCharactersAsync()
    {
        State = OperationState<IReadOnlyList<CharacterEntity>>.ToLoading();
        try
        {
            var query = _context.Characters.AsNoTracking();
            var activeAllyCode = _activeAllyCodeProvider?.Invoke()?.Trim();
            if (_activeAllyCodeProvider != null && string.IsNullOrWhiteSpace(activeAllyCode))
            {
                query = query.Where(character => false);
            }
            else if (!string.IsNullOrWhiteSpace(activeAllyCode))
            {
                query = query.Where(character => character.PlayerAllyCode == activeAllyCode);
            }

            var units = await query
                .OrderBy(character => character.Name)
                .ToListAsync()
                .ConfigureAwait(true);
            Characters.Clear();
            foreach (var unit in units)
            {
                Characters.Add(unit);
            }

            State = units.Count == 0
                ? OperationState<IReadOnlyList<CharacterEntity>>.ToEmpty()
                : OperationState<IReadOnlyList<CharacterEntity>>.ToSuccess(units);
            RebuildBoard();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading character priorities: {ex.Message}");
            State = OperationState<IReadOnlyList<CharacterEntity>>.ToError($"Failed to load priorities: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves a unit to a tier and position, then saves the full visible board order.
    /// </summary>
    public async Task MoveCharacterAsync(
        CharacterEntity character,
        PriorityTierViewModel destination,
        int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(destination);

        var source = FindTier(character);
        source?.Characters.Remove(character);
        destinationIndex = Math.Clamp(destinationIndex, 0, destination.Characters.Count);
        destination.Characters.Insert(destinationIndex, character);

        try
        {
            await PersistBoardAsync().ConfigureAwait(true);
            await NotifyPrioritiesChangedAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving tier-list priorities: {ex.Message}");
            await LoadCharactersAsync().ConfigureAwait(true);
            State = OperationState<IReadOnlyList<CharacterEntity>>.ToError($"Failed to save tier placement: {ex.Message}");
        }
    }

    protected override void OnStateChanged()
    {
        OnPropertyChanged(nameof(HasCharacters));
        OnPropertyChanged(nameof(IsActiveBoardEmpty));
    }

    private void RebuildBoard()
    {
        foreach (var tier in RankedTiers)
        {
            tier.Characters.Clear();
        }

        UnrankedTier.Characters.Clear();
        foreach (var unit in Characters
                     .Where(unit => _unitClassifier.IsShip(unit.Id) == ShowShips)
                     .OrderBy(unit => GetEffectiveTier(unit))
                     .ThenBy(unit => unit.PriorityOrder)
                     .ThenByDescending(unit => unit.Priority)
                     .ThenBy(unit => unit.Name))
        {
            FindTier(GetEffectiveTier(unit)).Characters.Add(unit);
        }

        OnPropertyChanged(nameof(IsActiveBoardEmpty));
    }

    private async Task PersistBoardAsync()
    {
        var displayedUnits = RankedTiers
            .Append(UnrankedTier)
            .SelectMany(tier => tier.Characters.Select((unit, index) => new BoardPosition(unit, tier.Tier, index)))
            .ToArray();
        foreach (var position in displayedUnits)
        {
            position.Unit.PriorityTier = position.Tier;
            position.Unit.PriorityOrder = position.Index;
            position.Unit.Priority = CalculatePriority(position.Tier, position.Index);
        }

        foreach (var account in displayedUnits.GroupBy(position => position.Unit.PlayerAllyCode, StringComparer.Ordinal))
        {
            var ids = account.Select(position => position.Unit.Id).ToArray();
            var persisted = await _context.Characters
                .Where(unit => unit.PlayerAllyCode == account.Key && ids.Contains(unit.Id))
                .ToDictionaryAsync(unit => unit.Id, StringComparer.Ordinal)
                .ConfigureAwait(true);
            foreach (var position in account)
            {
                if (!persisted.TryGetValue(position.Unit.Id, out var entity))
                {
                    continue;
                }

                entity.PriorityTier = position.Tier;
                entity.PriorityOrder = position.Index;
                entity.Priority = position.Unit.Priority;
            }
        }

        await _context.SaveChangesAsync().ConfigureAwait(true);
    }

    private async Task NotifyPrioritiesChangedAsync()
    {
        var handlers = PrioritiesChanged;
        if (handlers == null)
        {
            return;
        }

        var notifications = handlers
            .GetInvocationList()
            .Cast<Func<Task>>()
            .Select(handler => handler());
        await Task.WhenAll(notifications).ConfigureAwait(true);
    }

    private PriorityTierViewModel FindTier(CharacterEntity character) => FindTier(GetEffectiveTier(character));

    private PriorityTierViewModel FindTier(PriorityTier tier) => tier == PriorityTier.Unranked
        ? UnrankedTier
        : RankedTiers.Single(candidate => candidate.Tier == tier);

    private static PriorityTier GetEffectiveTier(CharacterEntity unit)
    {
        return unit.PriorityTier != PriorityTier.Unranked || unit.Priority == 0
            ? unit.PriorityTier
            : unit.Priority switch
            {
                >= 80 => PriorityTier.S,
                >= 60 => PriorityTier.A,
                >= 40 => PriorityTier.B,
                >= 20 => PriorityTier.C,
                _ => PriorityTier.D
            };
    }

    private static int CalculatePriority(PriorityTier tier, int index) => tier switch
    {
        PriorityTier.S => 100_000 - index,
        PriorityTier.A => 80_000 - index,
        PriorityTier.B => 60_000 - index,
        PriorityTier.C => 40_000 - index,
        PriorityTier.D => 20_000 - index,
        _ => 0
    };

    private sealed record BoardPosition(CharacterEntity Unit, PriorityTier Tier, int Index);
}
