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
using swgoh_command_bridge.UI.Controls;

namespace swgoh_command_bridge.UI.ViewModels;

public class ModsViewModel : StateViewModelBase<IReadOnlyList<GameModEntity>>
{
    private readonly AppDbContext _context;
    private readonly IModAdvisorService _advisorService;
    private readonly Func<ModUpgradeThreshold?> _thresholdProvider;
    private readonly Func<string?>? _activeAllyCodeProvider;
    private readonly List<GameModEntity> _allMods = new();
    private readonly List<GameModEntity> _filteredModResults = new();
    private readonly List<Character> _characters = new();
    private string _searchText = string.Empty;
    private string _headerText = "Mods · Inventory";
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
    private int _currentPage = 1;
    private int _recommendationVersion;
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
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        PreviousPageCommand = new RelayCommand(PreviousPage);
        NextPageCommand = new RelayCommand(NextPage);
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
            "Offense",
            "Defense",
            "CriticalChance",
            "Accuracy",
            "CriticalAvoidance",
            "CriticalChancePercent",
            "CriticalAvoidancePercent",
            "HealthPercent",
            "ProtectionPercent",
            "OffensePercent",
            "DefensePercent",
            "SpeedPercent"
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
            OnPropertyChanged(nameof(SelectedModVisualRequest));
            OnPropertyChanged(nameof(SelectedModSetText));
            OnPropertyChanged(nameof(SelectedModSlotText));
            OnPropertyChanged(nameof(SelectedModSummaryText));
            SelectedSecondarySummaries.Clear();
            if (value != null)
            {
                foreach (var summary in ParseSecondarySummaries(value.SecondaryStatsJson))
                {
                    SelectedSecondarySummaries.Add(summary);
                }
            }

            OnPropertyChanged(nameof(HasSelectedSecondaryStats));
            OnPropertyChanged(nameof(HasNoSelectedSecondaryStats));
            _ = UpdateRecommendationAsync();
        }
    }

    public ModVisualRequest? SelectedModVisualRequest => _selectedMod == null
        ? null
        : ModVisualRequest.FromEntity(_selectedMod);

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

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        !string.IsNullOrWhiteSpace(SecondaryFilter) ||
        RarityFilter > 0 ||
        SlotFilter > 0 ||
        SetFilter > 0 ||
        EquippedFilter > 0 ||
        !string.Equals(PrimaryFilter, "All primaries", StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(MinimumLevelFilter) ||
        !string.IsNullOrWhiteSpace(TierFilter);

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

    public bool HasMods => State.Status == OperationStatus.Success && _allMods.Count > 0;

    public bool HasFilteredMods => HasMods && FilteredMods.Count > 0;

    public bool HasNoMatchingMods => HasMods && FilteredMods.Count == 0;


    protected override void OnStateChanged()
    {
        OnPropertyChanged(nameof(HasMods));
        OnPropertyChanged(nameof(HasFilteredMods));
        OnPropertyChanged(nameof(HasNoMatchingMods));
        OnPropertyChanged(nameof(FilterSummaryText));
    }

    public string ActiveThresholdText =>
        $"Advisor threshold: {_thresholdProvider()?.Name ?? "Standard Settings"}";

    public string FilterSummaryText => HasMods
        ? HasActiveFilters
            ? $"Showing {_filteredModResults.Count} of {_allMods.Count} cached mod(s)."
            : $"Showing all {_allMods.Count} cached mod(s)."
        : "No cached mods loaded.";

    public string SelectedModSetText => SelectedMod == null
        ? string.Empty
        : $"Set: {FormatSet(SelectedMod.Set)}";

    public string SelectedModSlotText => SelectedMod == null
        ? string.Empty
        : $"Slot: {FormatSlot(SelectedMod.Slot)}";

    public string SelectedModSummaryText => SelectedMod == null
        ? string.Empty
        : $"{SelectedMod.Rarity}-dot {FormatSet(SelectedMod.Set)} mod, level {SelectedMod.Level}, tier {SelectedMod.Tier}";

    public ObservableCollection<string> SelectedSecondarySummaries { get; } = new();

    public bool HasSelectedSecondaryStats => SelectedSecondarySummaries.Count > 0;

    public bool HasNoSelectedSecondaryStats => !HasSelectedSecondaryStats;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ClearFiltersCommand { get; }

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
            if (_activeAllyCodeProvider != null && string.IsNullOrWhiteSpace(activeAllyCode))
            {
                modsQuery = modsQuery.Where(mod => false);
            }
            else if (!string.IsNullOrWhiteSpace(activeAllyCode))
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
            SelectedMod = null;
            var charactersQuery = _context.Characters.AsNoTracking();
            if (_activeAllyCodeProvider != null && string.IsNullOrWhiteSpace(activeAllyCode))
            {
                charactersQuery = charactersQuery.Where(character => false);
            }
            else if (!string.IsNullOrWhiteSpace(activeAllyCode))
            {
                charactersQuery = charactersQuery.Where(character => character.PlayerAllyCode == activeAllyCode);
            }

            var characters = await charactersQuery
                .ToListAsync()
                .ConfigureAwait(true);

            var characterNames = characters
                .ToDictionary(character => character.Id, character => character.Name, StringComparer.Ordinal);
            var charactersById = characters
                .ToDictionary(character => character.Id, StringComparer.Ordinal);
            foreach (var mod in mods)
            {
                mod.OwnerCharacter = string.IsNullOrWhiteSpace(mod.CharacterId)
                    ? null
                    : charactersById.GetValueOrDefault(mod.CharacterId);
                mod.OwnerDisplayName = string.IsNullOrWhiteSpace(mod.CharacterId)
                    ? "Un-equipped"
                    : characterNames.TryGetValue(mod.CharacterId, out var ownerName)
                        ? ownerName
                        : mod.CharacterId;
                PopulateDisplayFields(mod);
            }

            var equippedModsByCharacter = mods
                .Where(mod => !string.IsNullOrWhiteSpace(mod.CharacterId))
                .GroupBy(mod => mod.CharacterId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            _characters.Clear();
            foreach (var character in characters)
            {
                equippedModsByCharacter.TryGetValue(character.Id, out var equippedMods);
                _characters.Add(PersistedModelMapper.ToCharacter(
                    character,
                    equippedMods ?? new List<GameModEntity>()));
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
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(FilterSummaryText));
    }

    private void ClearFilters()
    {
        _searchText = string.Empty;
        _secondaryFilter = string.Empty;
        _minimumLevelFilter = string.Empty;
        _tierFilter = string.Empty;
        _rarityFilter = 0;
        _slotFilter = 0;
        _setFilter = 0;
        _equippedFilter = 0;
        _primaryFilter = "All primaries";
        _sortOption = "Rarity";

        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SecondaryFilter));
        OnPropertyChanged(nameof(MinimumLevelFilter));
        OnPropertyChanged(nameof(TierFilter));
        OnPropertyChanged(nameof(RarityFilter));
        OnPropertyChanged(nameof(SlotFilter));
        OnPropertyChanged(nameof(SetFilter));
        OnPropertyChanged(nameof(EquippedFilter));
        OnPropertyChanged(nameof(PrimaryFilter));
        OnPropertyChanged(nameof(SortOption));
        OnPropertyChanged(nameof(SecondaryFilterError));
        OnPropertyChanged(nameof(HasSecondaryFilterError));
        ApplyFiltersAndSort();
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
        OnPropertyChanged(nameof(FilterSummaryText));
    }

    private async Task UpdateRecommendationAsync()
    {
        var version = ++_recommendationVersion;
        if (SelectedMod == null)
        {
            SelectedModRecommendation = null;
            return;
        }

        var selectedMod = SelectedMod;

        var threshold = _thresholdProvider() ?? new ModUpgradeThreshold(
                "default",
                "Standard Settings",
                MinimumRarity: 5,
                MinimumTier: 4,
                MinimumSpeed: 10,
                UpgradeOnlyWithSpeed: true,
                MinimumEfficiency: 0.0);

        var recommendation = await _advisorService.AnalyzeModAsync(
            PersistedModelMapper.ToGameMod(selectedMod),
            threshold,
            _characters);
        if (version == _recommendationVersion && ReferenceEquals(SelectedMod, selectedMod))
        {
            SelectedModRecommendation = recommendation;
        }
    }

    private static IReadOnlyList<string> ParseSecondarySummaries(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var snapshots = JsonSerializer.Deserialize<List<ModStatSnapshot>>(json) ?? new List<ModStatSnapshot>();
            return snapshots
                .Where(snapshot => Enum.TryParse<StatType>(snapshot.Type, true, out _))
                .Select(snapshot =>
                {
                    Enum.TryParse<StatType>(snapshot.Type, true, out var type);
                    return new ModStat(type, snapshot.Value, snapshot.RollCount).ToString();
                })
                .ToList()
                .AsReadOnly();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string FormatSet(int set) =>
        Enum.IsDefined(typeof(ModSet), set) ? ((ModSet)set).ToString() : $"Set {set}";

    private static string FormatSlot(int slot) =>
        Enum.IsDefined(typeof(ModSlot), slot) ? ((ModSlot)slot).ToString() : $"Slot {slot}";

    private static void PopulateDisplayFields(GameModEntity mod)
    {
        mod.QualitySummary = $"{mod.Rarity}-dot • Level {mod.Level} • Tier {mod.Tier}";
        mod.SetSlotSummary = $"{FormatSet(mod.Set)} • {FormatSlot(mod.Slot)}";
        mod.PrimaryStatSummary = FormatPrimaryStat(mod.PrimaryStatType, mod.PrimaryStatValue);

        var secondaries = ParseSecondarySummaries(mod.SecondaryStatsJson);
        mod.SecondaryStatsSummary = secondaries.Count switch
        {
            0 => "No readable secondary stats",
            1 => secondaries[0],
            2 => string.Join(" • ", secondaries),
            _ => string.Join(" • ", secondaries.Take(2)) + $" • +{secondaries.Count - 2} more"
        };
    }

    private static string FormatPrimaryStat(string? statType, double value)
    {
        if (Enum.TryParse<StatType>(statType, true, out var type))
        {
            return $"Primary: {new ModStat(type, value).ToString()}";
        }

        return string.IsNullOrWhiteSpace(statType) ||
               string.Equals(statType, "None", StringComparison.OrdinalIgnoreCase)
            ? "Primary: unavailable"
            : $"Primary: {statType} {value:F2}";
    }

}
