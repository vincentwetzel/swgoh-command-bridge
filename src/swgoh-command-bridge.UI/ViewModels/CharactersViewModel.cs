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

namespace swgoh_command_bridge.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing the character collection and priority scoring page.
    /// </summary>
    public class CharactersViewModel : StateViewModelBase<IReadOnlyList<CharacterEntity>>
    {
        private readonly AppDbContext _context;
        private readonly Func<string?>? _activeAllyCodeProvider;
        private readonly ICharacterCatalogService? _characterCatalogService;
        private readonly DiagnosticEventLog? _eventLog;
        private readonly IPreferredModsDatasetService? _preferredModsService;
        private bool _catalogRepairAttempted;
        private bool _catalogRepairInProgress;
        private string _headerText = "Characters List";
        private string _searchText = string.Empty;
        private string _catalogStatusText = string.Empty;
        private string _preferredModDataText = "Top GAC mod data is unavailable.";
        private CharacterEntity? _selectedCharacter;
        private readonly List<GameModEntity> _equippedMods = new();
        /// <summary>
        /// Gets the collection of characters loaded from the database.
        /// </summary>
        public ObservableCollection<CharacterEntity> Characters { get; } = new();

        /// <summary>
        /// Gets the currently selected character.
        /// </summary>
        public CharacterEntity? SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                if (ReferenceEquals(_selectedCharacter, value))
                {
                    return;
                }

                _selectedCharacter = value;
                OnPropertyChanged(nameof(SelectedCharacter));
                OnPropertyChanged(nameof(HasSelectedCharacter));
                CurrentMods.Clear();
                if (value != null)
                {
                    foreach (var mod in _equippedMods.Where(mod =>
                                 string.Equals(mod.CharacterId, value.Id, StringComparison.Ordinal)))
                    {
                        CurrentMods.Add(mod);
                    }
                }

                OnPropertyChanged(nameof(HasCurrentMods));
                OnPropertyChanged(nameof(HasNoCurrentMods));
                RefreshPreferredModGuidance();
            }
        }

        /// <summary>
        /// Gets the equipped mods for the selected character.
        /// </summary>
        public ObservableCollection<GameModEntity> CurrentMods { get; } = new();

        /// <summary>Common complete builds observed in the top GAC dataset.</summary>
        public ObservableCollection<string> PreferredSetups { get; } = new();

        /// <summary>Primary-stat distributions for each mod slot.</summary>
        public ObservableCollection<string> PreferredPrimaryAdvice { get; } = new();

        /// <summary>Comparison of the selected character's equipped or missing mods to the preferred data.</summary>
        public ObservableCollection<string> CurrentModGuidance { get; } = new();

        public bool HasSelectedCharacter => SelectedCharacter != null;

        public bool HasCurrentMods => CurrentMods.Count > 0;

        public bool HasNoCurrentMods => !HasCurrentMods;

        public bool HasPreferredSetups => PreferredSetups.Count > 0;

        public bool HasPreferredPrimaryAdvice => PreferredPrimaryAdvice.Count > 0;

        public bool HasCurrentModGuidance => CurrentModGuidance.Count > 0;

        public string PreferredModDataText
        {
            get => _preferredModDataText;
            private set
            {
                if (_preferredModDataText != value)
                {
                    _preferredModDataText = value;
                    OnPropertyChanged(nameof(PreferredModDataText));
                }
            }
        }

        /// <summary>
        /// Gets or sets the header text for the characters panel.
        /// </summary>
        public string HeaderText
        {
            get => _headerText;
            set
            {
                if (_headerText != value)
                {
                    _headerText = value;
                    OnPropertyChanged(nameof(HeaderText));
                }
            }
        }

        /// <summary>
        /// Gets or sets the text used to search and filter the character collection.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    _ = LoadCharactersAsync();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether data is currently being retrieved.
        /// </summary>
        public bool HasCharacters => State.Status == OperationStatus.Success;

        protected override void OnStateChanged() =>
            OnPropertyChanged(nameof(HasCharacters));

        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CharactersViewModel"/> class.
        /// </summary>
        public CharactersViewModel()
            : this(new AppDbContext(), null)
        {
        }

        /// <summary>
        /// Gets the outcome of the most recent authoritative catalog refresh.
        /// </summary>
        public string CatalogStatusText
        {
            get => _catalogStatusText;
            private set
            {
                if (_catalogStatusText != value)
                {
                    _catalogStatusText = value;
                    OnPropertyChanged(nameof(CatalogStatusText));
                    OnPropertyChanged(nameof(HasCatalogStatus));
                }
            }
        }

        public bool HasCatalogStatus => !string.IsNullOrWhiteSpace(CatalogStatusText);

        /// <summary>
        /// Initializes a new instance of the <see cref="CharactersViewModel"/> class.
        /// </summary>
        public CharactersViewModel(AppDbContext context)
            : this(context, null)
        {
        }

        public CharactersViewModel(AppDbContext context, Func<string?>? activeAllyCodeProvider)
            : this(context, activeAllyCodeProvider, null, null)
        {
        }

        public CharactersViewModel(
            AppDbContext context,
            Func<string?>? activeAllyCodeProvider,
            ICharacterCatalogService? characterCatalogService,
            DiagnosticEventLog? eventLog = null,
            IPreferredModsDatasetService? preferredModsService = null)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _activeAllyCodeProvider = activeAllyCodeProvider;
            _characterCatalogService = characterCatalogService;
            _eventLog = eventLog;
            _preferredModsService = preferredModsService;
            if (_preferredModsService != null)
            {
                _preferredModsService.DatasetChanged += (_, _) => RefreshPreferredModGuidance();
            }
            RefreshCommand = new AsyncRelayCommand(LoadCharactersAsync);
        }

        /// <summary>
        /// Asynchronously retrieves character lists matching the search filter criteria.
        /// </summary>
        public async Task LoadCharactersAsync()
        {
            State = OperationState<IReadOnlyList<CharacterEntity>>.ToLoading();
            try
            {
                await RepairCachedCharacterCatalogAsync().ConfigureAwait(true);
                var query = _context.Characters.AsNoTracking();
                var activeAllyCode = _activeAllyCodeProvider?.Invoke()?.Trim();
                if (_activeAllyCodeProvider != null && string.IsNullOrWhiteSpace(activeAllyCode))
                {
                    query = query.Where(character => false);
                }
                else if (!string.IsNullOrWhiteSpace(activeAllyCode))
                {
                    query = query.Where(c => c.PlayerAllyCode == activeAllyCode);
                }

                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    var normalizedSearch = _searchText.ToUpperInvariant();
                    query = query.Where(c => c.Name.ToUpper().Contains(normalizedSearch));
                }

                var list = await query
                    .OrderByDescending(c => c.Priority)
                    .ThenBy(c => c.Name)
                    .ToListAsync()
                    .ConfigureAwait(true);

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
                    .Where(mod => !string.IsNullOrWhiteSpace(mod.CharacterId))
                    .OrderBy(mod => mod.Slot)
                    .ThenBy(mod => mod.Id)
                    .ToListAsync()
                    .ConfigureAwait(true);

                foreach (var mod in mods)
                {
                    PopulateDisplayFields(mod);
                }

                var selectedCharacterId = SelectedCharacter?.Id;
                _equippedMods.Clear();
                _equippedMods.AddRange(mods);

                Characters.Clear();
                foreach (var character in list)
                {
                    Characters.Add(character);
                }

                if (Characters.Count == 0)
                {
                    SelectedCharacter = null;
                    State = OperationState<IReadOnlyList<CharacterEntity>>.ToEmpty();
                }
                else
                {
                    State = OperationState<IReadOnlyList<CharacterEntity>>.ToSuccess(list);
                    SelectedCharacter = Characters.FirstOrDefault(character =>
                        string.Equals(character.Id, selectedCharacterId, StringComparison.Ordinal))
                        ?? Characters.First();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading characters: {ex.Message}");
                State = OperationState<IReadOnlyList<CharacterEntity>>.ToError($"Failed to load characters: {ex.Message}");
            }
        }

        /// <summary>
        /// Reapplies the active catalog after a verified local snapshot import.
        /// </summary>
        public async Task RefreshCatalogAsync()
        {
            _catalogRepairAttempted = false;
            await LoadCharactersAsync().ConfigureAwait(true);
        }

        private async Task RepairCachedCharacterCatalogAsync()
        {
            if (_catalogRepairInProgress)
            {
                return;
            }

            _catalogRepairInProgress = true;
            try
            {
                if (_catalogRepairAttempted || _characterCatalogService == null)
                {
                    return;
                }

                CatalogStatusText = "Updating character names and portraits from authoritative catalog data...";
                var catalogPayload = await _characterCatalogService
                    .FetchCharacterCatalogAsync()
                    .ConfigureAwait(true);
                var parseResult = new CharacterCatalogParser().ParseWithAudit(catalogPayload);
                var catalog = parseResult.Entries;
                _eventLog?.Info(
                    "character-catalog",
                    $"source={catalogPayload.Source}; {parseResult.Audit.Summary}");
                if (parseResult.Audit.Entries == 0 || parseResult.Audit.EntriesWithNames == 0)
                {
                    throw new InvalidOperationException(
                        $"{catalogPayload.Source} returned a catalog without character names: {parseResult.Audit.Summary}");
                }

                var cachedCharacters = await _context.Characters
                    .ToListAsync()
                    .ConfigureAwait(true);
                var matched = 0;
                var changed = 0;
                var missingPortraits = 0;
                foreach (var character in cachedCharacters)
                {
                    if (!catalog.TryGetValue(character.Id, out var entry))
                    {
                        continue;
                    }

                    matched++;
                    if (!string.Equals(character.Name, entry.Name, StringComparison.Ordinal) ||
                        !string.Equals(character.PortraitAsset, entry.PortraitAsset, StringComparison.Ordinal))
                    {
                        changed++;
                        character.Name = entry.Name;
                        character.PortraitAsset = entry.PortraitAsset;
                    }

                    if (string.IsNullOrWhiteSpace(entry.PortraitAsset))
                    {
                        missingPortraits++;
                    }
                }

                if (changed > 0)
                {
                    await _context.SaveChangesAsync().ConfigureAwait(true);
                }

                var unmatched = cachedCharacters.Count - matched;
                var auditMessage =
                    $"cache rows={cachedCharacters.Count}, matched={matched}, updated={changed}, " +
                    $"unmatched={unmatched}, matched rows without portrait={missingPortraits}";
                if (unmatched > 0 || missingPortraits > 0)
                {
                    _eventLog?.Warning("character-catalog", auditMessage);
                }
                else
                {
                    _eventLog?.Info("character-catalog", auditMessage);
                }

                CatalogStatusText = unmatched == 0 && missingPortraits == 0
                    ? $"Character catalog updated from {catalogPayload.Source}: {matched} roster entries verified."
                    : $"Character catalog updated from {catalogPayload.Source}: {matched} matched; {unmatched} unavailable; {missingPortraits} without artwork.";
                _catalogRepairAttempted = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Character catalog repair skipped: {ex.Message}");
                _eventLog?.Error("character-catalog", $"Catalog repair failed: {ex.Message}");
                CatalogStatusText =
                    $"Character catalog could not be loaded ({ex.GetType().Name}). Showing cached data. " +
                    "See Diagnostics or application-events.log for details.";
            }
            finally
            {
                _catalogRepairInProgress = false;
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
                return $"Primary: {new ModStat(type, value)}";
            }

            return string.IsNullOrWhiteSpace(statType) ||
                   string.Equals(statType, "None", StringComparison.OrdinalIgnoreCase)
                ? "Primary: unavailable"
                : $"Primary: {statType} {value:F2}";
        }

        private void RefreshPreferredModGuidance()
        {
            PreferredSetups.Clear();
            PreferredPrimaryAdvice.Clear();
            CurrentModGuidance.Clear();

            if (_preferredModsService == null)
            {
                PreferredModDataText = "Top GAC mod data is not configured.";
                NotifyPreferredModGuidanceChanged();
                return;
            }

            var info = _preferredModsService.GetDatasetInfo();
            if (SelectedCharacter == null)
            {
                PreferredModDataText = info.CompactSummary;
                NotifyPreferredModGuidanceChanged();
                return;
            }

            var recommendation = _preferredModsService.Current.Characters.FirstOrDefault(character =>
                string.Equals(character.CharacterId, SelectedCharacter.Id, StringComparison.OrdinalIgnoreCase));
            if (recommendation == null)
            {
                PreferredModDataText = $"{info.CompactSummary} · No top GAC build data for this character yet.";
                NotifyPreferredModGuidanceChanged();
                return;
            }

            PreferredModDataText =
                $"{info.CompactSummary} · {recommendation.SampleSize:N0} character samples · {recommendation.Confidence} confidence";
            foreach (var setup in recommendation.Setups.Take(3))
            {
                var sets = string.Join(" + ", setup.Sets.Select(set => $"{set.Count} {set.Set}"));
                PreferredSetups.Add($"{sets} ({setup.Share:P0})");
            }

            foreach (var slot in recommendation.Slots.OrderBy(slot => slot.Slot))
            {
                var preferred = slot.Options.FirstOrDefault(option =>
                    option.Status == PreferredRecommendationStatus.Preferred);
                var displayOptions = preferred == null
                    ? slot.Options.Take(3).ToList()
                    : slot.Options
                        .Where(option => option.Status is PreferredRecommendationStatus.Preferred or
                            PreferredRecommendationStatus.ViableAlternative)
                        .ToList();
                if (displayOptions.Count == 0 && slot.Options.FirstOrDefault() is { } fallback)
                {
                    displayOptions.Add(fallback);
                }

                var options = string.Join(", ", displayOptions.Select(option =>
                    $"{FormatPreferredStat(option.PrimaryStat)} {option.Share:P0} ({FormatStatus(option.Status)})"));
                PreferredPrimaryAdvice.Add($"{slot.Slot}: {options}");

                var currentMod = CurrentMods.FirstOrDefault(mod => mod.Slot == (int)slot.Slot);
                if (preferred == null)
                {
                    if (currentMod == null)
                    {
                        CurrentModGuidance.Add($"{slot.Slot}: no clear primary consensus; use one of the listed options.");
                    }

                    continue;
                }

                if (currentMod == null)
                {
                    CurrentModGuidance.Add(
                        $"{slot.Slot}: no mod equipped — use {FormatPreferredStat(preferred.PrimaryStat)}.");
                    continue;
                }

                var currentPrimary = Enum.TryParse<StatType>(currentMod.PrimaryStatType, true, out var parsed)
                    ? parsed
                    : StatType.None;
                var currentOption = slot.Options.FirstOrDefault(option => option.PrimaryStat == currentPrimary);
                if (currentOption?.Status == PreferredRecommendationStatus.Preferred)
                {
                    continue;
                }
                else if (currentOption?.Status == PreferredRecommendationStatus.ViableAlternative)
                {
                    continue;
                }
                else
                {
                    CurrentModGuidance.Add(
                        $"{slot.Slot}: consider {FormatPreferredStat(preferred.PrimaryStat)} instead of {FormatPreferredStat(currentPrimary)}.");
                }
            }

            NotifyPreferredModGuidanceChanged();
        }

        private void NotifyPreferredModGuidanceChanged()
        {
            OnPropertyChanged(nameof(HasPreferredSetups));
            OnPropertyChanged(nameof(HasPreferredPrimaryAdvice));
            OnPropertyChanged(nameof(HasCurrentModGuidance));
        }

        private static string FormatPreferredStat(StatType stat) => stat == StatType.None
            ? "an unknown primary"
            : stat.ToString().Replace("Percent", " %", StringComparison.Ordinal);

        private static string FormatStatus(PreferredRecommendationStatus status) => status switch
        {
            PreferredRecommendationStatus.Preferred => "preferred",
            PreferredRecommendationStatus.ViableAlternative => "viable",
            PreferredRecommendationStatus.Inconclusive => "limited data",
            PreferredRecommendationStatus.LessCommon => "less common",
            _ => "unavailable"
        };

    }
}
