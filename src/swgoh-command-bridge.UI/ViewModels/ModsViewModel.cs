#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI.ViewModels;

public class ModsViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    private readonly IModAdvisorService _advisorService;
    private readonly Func<ModUpgradeThreshold?> _thresholdProvider;
    private readonly Func<string?>? _activeAllyCodeProvider;
    private readonly List<GameModEntity> _allMods = new();
    private readonly List<GameModEntity> _filteredModResults = new();
    private readonly List<Character> _characters = new();
    private string _searchText = string.Empty;
    private string _headerText = "Mod Inventory";
    private GameModEntity? _selectedMod;
    private ModRecommendation? _selectedModRecommendation;
    private int _rarityFilter;
    private int _slotFilter;
    private int _setFilter;
    private int _equippedFilter;
    private string _primaryFilter = "All primaries";
    private string _secondaryFilter = string.Empty;
    private string _minimumLevelFilter = string.Empty;
    private string _tierFilter = string.Empty;
    private string _sortOption = "Rarity";
    private OperationState<IReadOnlyList<GameModEntity>> _state =
        OperationState<IReadOnlyList<GameModEntity>>.ToEmpty();
    private int _currentPage = 1;
    private const int PageSize = 100;

    public ModsViewModel(AppDbContext context, IModAdvisorService advisorService)
        : this(context, advisorService, null, null)
    {
    }

    public ModsViewModel(
        AppDbContext context,
        IModAdvisorService advisorService,
        Func<ModUpgradeThreshold?>? thresholdProvider,
        Func<string?>? activeAllyCodeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(advisorService);

        _context = context;
        _advisorService = advisorService;
        _thresholdProvider = thresholdProvider ?? (() => null);
        _activeAllyCodeProvider = activeAllyCodeProvider;
        RefreshCommand = new AsyncRelayCommand(LoadModsAsync);
        PreviousPageCommand = new RelayCommand(PreviousPage);
        NextPageCommand = new RelayCommand(NextPage);
    }

    public OperationState<IReadOnlyList<GameModEntity>> State
    {
        get => _state;
        private set
        {
            _state = value;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasMods));
            OnPropertyChanged(nameof(HasFilteredMods));
            OnPropertyChanged(nameof(HasNoMatchingMods));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorMessage));
        }
    }

    public ObservableCollection<GameModEntity> FilteredMods { get; } = new();

    public IReadOnlyList<string> PrimaryStatOptions { get; } =
        new[]
        {
            "All primaries",
            "Health",
            "Strength",
            "Agility",
            "Tactics",
            "Speed",
            "PhysicalDamage",
            "SpecialDamage",
            "Armor",
            "Resistance",
            "ArmorPenetration",
            "ResistancePenetration",
            "DodgeChance",
            "DeflectionChance",
            "PhysicalCriticalChance",
            "SpecialCriticalChance",
            "CriticalDamage",
            "Potency",
            "Tenacity",
            "HealthSteal",
            "Protection",
            "Accuracy",
            "CriticalAvoidance",
            "HealthPercent",
            "ProtectionPercent",
            "OffensePercent",
            "DefensePercent"
        };

    public IReadOnlyList<string> SortOptions { get; } =
        new[] { "Rarity", "Level", "Tier", "Slot", "Set", "Primary" };

    public string HeaderText
    {
        get => _headerText;
        set
        {
            if (_headerText == value)
            {
                return;
            }

            _headerText = value;
            OnPropertyChanged(nameof(HeaderText));
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            ApplyFiltersAndSort();
        }
    }

    public GameModEntity? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (_selectedMod == value)
            {
                return;
            }

            _selectedMod = value;
            OnPropertyChanged(nameof(SelectedMod));
            _ = UpdateRecommendationAsync();
        }
    }

    public ModRecommendation? SelectedModRecommendation
    {
        get => _selectedModRecommendation;
        private set
        {
            if (_selectedModRecommendation == value)
            {
                return;
            }

            _selectedModRecommendation = value;
            OnPropertyChanged(nameof(SelectedModRecommendation));
        }
    }

    public string SortOption
    {
        get => _sortOption;
        set
        {
            if (_sortOption == value)
            {
                return;
            }

            _sortOption = value;
            OnPropertyChanged(nameof(SortOption));
            ApplyFiltersAndSort();
        }
    }

    public int RarityFilter
    {
        get => _rarityFilter;
        set
        {
            if (_rarityFilter == value)
            {
                return;
            }

            _rarityFilter = value;
            OnPropertyChanged(nameof(RarityFilter));
            ApplyFiltersAndSort();
        }
    }

    public int SlotFilter
    {
        get => _slotFilter;
        set
        {
            if (_slotFilter == value)
            {
                return;
            }

            _slotFilter = value;
            OnPropertyChanged(nameof(SlotFilter));
            ApplyFiltersAndSort();
        }
    }

    public int SetFilter
    {
        get => _setFilter;
        set
        {
            if (_setFilter == value)
            {
                return;
            }

            _setFilter = value;
            OnPropertyChanged(nameof(SetFilter));
            ApplyFiltersAndSort();
        }
    }

    public int EquippedFilter
    {
        get => _equippedFilter;
        set
        {
            if (_equippedFilter == value)
            {
                return;
            }

            _equippedFilter = value;
            OnPropertyChanged(nameof(EquippedFilter));
            ApplyFiltersAndSort();
        }
    }

    public string PrimaryFilter
    {
        get => _primaryFilter;
        set
        {
            if (_primaryFilter == value)
            {
                return;
            }

            _primaryFilter = value;
            OnPropertyChanged(nameof(PrimaryFilter));
            ApplyFiltersAndSort();
        }
    }

    public string SecondaryFilter
    {
        get => _secondaryFilter;
        set
        {
            if (_secondaryFilter == value)
            {
                return;
            }

            _secondaryFilter = value;
            OnPropertyChanged(nameof(SecondaryFilter));
            OnPropertyChanged(nameof(SecondaryFilterError));
            OnPropertyChanged(nameof(HasSecondaryFilterError));
            ApplyFiltersAndSort();
        }
    }

    public string SecondaryFilterError =>
        SecondaryStatFilterService.TryParse(SecondaryFilter, out _, out var error)
            ? string.Empty
            : error ?? "Invalid secondary-stat filter.";

    public bool HasSecondaryFilterError => !string.IsNullOrWhiteSpace(SecondaryFilterError);

    public string MinimumLevelFilter
    {
        get => _minimumLevelFilter;
        set
        {
            if (_minimumLevelFilter == value)
            {
                return;
            }

            _minimumLevelFilter = value;
            OnPropertyChanged(nameof(MinimumLevelFilter));
            ApplyFiltersAndSort();
        }
    }

    public string TierFilter
    {
        get => _tierFilter;
        set
        {
            if (_tierFilter == value)
            {
                return;
            }

            _tierFilter = value;
            OnPropertyChanged(nameof(TierFilter));
            ApplyFiltersAndSort();
        }
    }

    public bool IsLoading => State.Status == OperationStatus.Loading;

    public bool IsEmpty => State.Status == OperationStatus.Empty;

    public bool HasMods => State.Status == OperationStatus.Success && _allMods.Count > 0;

    public bool HasFilteredMods => HasMods && FilteredMods.Count > 0;

    public bool HasNoMatchingMods => HasMods && FilteredMods.Count == 0;

    public bool HasError => State.Status == OperationStatus.Error;

    public string ErrorMessage => State.ErrorMessage ?? string.Empty;

    public string ActiveThresholdText =>
        $"Advisor threshold: {_thresholdProvider()?.Name ?? "Standard Settings"}";

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand PreviousPageCommand { get; }

    public IRelayCommand NextPageCommand { get; }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (_currentPage == value)
            {
                return;
            }

            _currentPage = value;
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(PageText));
            OnPropertyChanged(nameof(CanPreviousPage));
            OnPropertyChanged(nameof(CanNextPage));
        }
    }

    public int PageCount => Math.Max(1, (int)Math.Ceiling(_filteredModResults.Count / (double)PageSize));

    public bool CanPreviousPage => CurrentPage > 1;

    public bool CanNextPage => CurrentPage < PageCount;

    public string PageText => _filteredModResults.Count == 0
        ? "No matching mods"
        : $"Page {CurrentPage} of {PageCount} ({_filteredModResults.Count} matching mods)";

    public void RefreshThresholdContext()
    {
        OnPropertyChanged(nameof(ActiveThresholdText));
        if (SelectedMod != null)
        {
            _ = UpdateRecommendationAsync();
        }
    }

    public async Task LoadModsAsync()
    {
        State = OperationState<IReadOnlyList<GameModEntity>>.ToLoading();

        try
        {
            var activeAllyCode = _activeAllyCodeProvider?.Invoke()?.Trim();
            var modsQuery = _context.Mods.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(activeAllyCode))
            {
                modsQuery = modsQuery.Where(mod => mod.PlayerAllyCode == activeAllyCode);
            }

            var mods = await modsQuery
                .OrderByDescending(mod => mod.Rarity)
                .ThenByDescending(mod => mod.Level)
                .ToListAsync()
                .ConfigureAwait(true);

            _allMods.Clear();
            _allMods.AddRange(mods);
            var charactersQuery = _context.Characters.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(activeAllyCode))
            {
                charactersQuery = charactersQuery.Where(character => character.PlayerAllyCode == activeAllyCode);
            }

            var characters = await charactersQuery
                .ToListAsync()
                .ConfigureAwait(true);

            var characterNames = characters
                .ToDictionary(character => character.Id, character => character.Name, StringComparer.Ordinal);
            foreach (var mod in mods)
            {
                mod.OwnerDisplayName = string.IsNullOrWhiteSpace(mod.CharacterId)
                    ? "Un-equipped"
                    : characterNames.TryGetValue(mod.CharacterId, out var ownerName)
                        ? ownerName
                        : mod.CharacterId;
            }

            var equippedModsByCharacter = mods
                .Where(mod => !string.IsNullOrWhiteSpace(mod.CharacterId))
                .GroupBy(mod => mod.CharacterId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            _characters.Clear();
            foreach (var character in characters)
            {
                equippedModsByCharacter.TryGetValue(character.Id, out var equippedMods);
                _characters.Add(ToCharacter(character, equippedMods ?? new List<GameModEntity>()));
            }

            ApplyFiltersAndSort();
            State = mods.Count == 0
                ? OperationState<IReadOnlyList<GameModEntity>>.ToEmpty()
                : OperationState<IReadOnlyList<GameModEntity>>.ToSuccess(mods);
        }
        catch (Exception ex)
        {
            State = OperationState<IReadOnlyList<GameModEntity>>.ToError(
                $"Failed to load mods: {ex.Message}");
        }
    }

    private void ApplyFiltersAndSort()
    {
        IEnumerable<GameModEntity> query = _allMods;

        if (_rarityFilter > 0)
        {
            query = query.Where(mod => mod.Rarity == _rarityFilter);
        }

        if (_slotFilter > 0)
        {
            query = query.Where(mod => mod.Slot == _slotFilter);
        }

        if (_setFilter > 0)
        {
            query = query.Where(mod => mod.Set == _setFilter);
        }

        if (!string.Equals(_primaryFilter, "All primaries", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(mod => string.Equals(
                mod.PrimaryStatType,
                _primaryFilter,
                StringComparison.OrdinalIgnoreCase));
        }

        if (_equippedFilter == 1)
        {
            query = query.Where(mod => !string.IsNullOrWhiteSpace(mod.CharacterId));
        }
        else if (_equippedFilter == 2)
        {
            query = query.Where(mod => string.IsNullOrWhiteSpace(mod.CharacterId));
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            query = query.Where(mod =>
                mod.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                mod.CharacterId.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                mod.PrimaryStatType.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_secondaryFilter))
        {
            query = query.Where(mod => SecondaryStatFilterService.Matches(
                mod.SecondaryStatsJson,
                _secondaryFilter));
        }

        if (int.TryParse(_minimumLevelFilter, out var minimumLevel) && minimumLevel > 0)
        {
            query = query.Where(mod => mod.Level >= minimumLevel);
        }

        if (int.TryParse(_tierFilter, out var tier) && tier > 0)
        {
            query = query.Where(mod => mod.Tier == tier);
        }

        query = _sortOption switch
        {
            "Level" => query.OrderByDescending(mod => mod.Level).ThenBy(mod => mod.Id, StringComparer.Ordinal),
            "Tier" => query.OrderByDescending(mod => mod.Tier).ThenBy(mod => mod.Id, StringComparer.Ordinal),
            "Slot" => query.OrderBy(mod => mod.Slot).ThenBy(mod => mod.Id, StringComparer.Ordinal),
            "Set" => query.OrderBy(mod => mod.Set).ThenBy(mod => mod.Id, StringComparer.Ordinal),
            "Primary" => query.OrderBy(mod => mod.PrimaryStatType).ThenBy(mod => mod.Id, StringComparer.Ordinal),
            _ => query.OrderByDescending(mod => mod.Rarity).ThenByDescending(mod => mod.Level).ThenBy(mod => mod.Id, StringComparer.Ordinal)
        };

        _filteredModResults.Clear();
        _filteredModResults.AddRange(query);
        CurrentPage = 1;
        RenderCurrentPage();
    }

    private void PreviousPage()
    {
        if (CanPreviousPage)
        {
            CurrentPage--;
            RenderCurrentPage();
        }
    }

    private void NextPage()
    {
        if (CanNextPage)
        {
            CurrentPage++;
            RenderCurrentPage();
        }
    }

    private void RenderCurrentPage()
    {
        var pageCount = PageCount;
        if (CurrentPage > pageCount)
        {
            CurrentPage = pageCount;
        }

        FilteredMods.Clear();
        foreach (var mod in _filteredModResults
                     .Skip((CurrentPage - 1) * PageSize)
                     .Take(PageSize))
        {
            FilteredMods.Add(mod);
        }

        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(CanPreviousPage));
        OnPropertyChanged(nameof(CanNextPage));
        OnPropertyChanged(nameof(HasFilteredMods));
        OnPropertyChanged(nameof(HasNoMatchingMods));
    }

    private async Task UpdateRecommendationAsync()
    {
        if (SelectedMod == null)
        {
            SelectedModRecommendation = null;
            return;
        }

        var threshold = _thresholdProvider() ?? new ModUpgradeThreshold(
                "default",
                "Standard Settings",
                MinimumRarity: 5,
                MinimumTier: 4,
                MinimumSpeed: 10,
                UpgradeOnlyWithSpeed: true,
                MinimumEfficiency: 60.0);

        SelectedModRecommendation = await _advisorService.AnalyzeModAsync(
            ToGameMod(SelectedMod),
            threshold,
            _characters);
    }

    private static Character ToCharacter(
        CharacterEntity character,
        IReadOnlyList<GameModEntity> equippedMods)
    {
        var equipped = new Dictionary<ModSlot, GameMod>();
        foreach (var mod in equippedMods)
        {
            if (!Enum.IsDefined(typeof(ModSlot), mod.Slot))
            {
                continue;
            }

            equipped[(ModSlot)mod.Slot] = ToGameMod(mod);
        }

        return new Character(
            character.Id,
            character.Name,
            character.Level,
            character.GearLevel,
            0,
            (int)Math.Clamp(character.GalacticPower, 0L, int.MaxValue),
            character.Priority,
            equipped)
        {
            Stars = character.Stars
        };
    }

    private static GameMod ToGameMod(GameModEntity mod)
    {
        var primaryType = Enum.TryParse<StatType>(mod.PrimaryStatType, true, out var parsedType)
            ? parsedType
            : StatType.None;
        var secondaries = new List<ModStat>();

        try
        {
            var snapshots = JsonSerializer.Deserialize<List<ModStatSnapshot>>(mod.SecondaryStatsJson);
            if (snapshots != null)
            {
                foreach (var snapshot in snapshots)
                {
                    if (Enum.TryParse<StatType>(snapshot.Type, true, out var secondaryType))
                    {
                        secondaries.Add(new ModStat(secondaryType, snapshot.Value, snapshot.RollCount));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // A malformed cached stat payload should not prevent inventory browsing.
        }

        return new GameMod(
            mod.Id,
            mod.Level,
            mod.Rarity,
            mod.Tier,
            (ModSlot)mod.Slot,
            (ModSet)mod.Set,
            new ModStat(primaryType, mod.PrimaryStatValue),
            secondaries,
            string.IsNullOrWhiteSpace(mod.CharacterId) ? null : mod.CharacterId);
    }
}
