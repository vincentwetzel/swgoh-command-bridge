#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
    /// ViewModel representing the mod optimization and recommendation interface.
    /// </summary>
    public class ModOptimizerViewModel : StateViewModelBase<IReadOnlyList<GameModEntity>>
    {
        private readonly AppDbContext _context;
        private readonly IModAssignmentService _assignmentService;
        private readonly ISwgohGgScraperService? _scraperService;
        private readonly ISettingsService? _settingsService;
        private readonly Func<string?>? _activeAllyCodeProvider;
        private CancellationTokenSource? _scrapeCancellation;
        private string _headerText = "Optimize · Mod Assignments";
        private CharacterEntity? _selectedCharacter;
        private string _popularityText = "No community recommendation data available.";
        private string _lastUpdatedText = string.Empty;
        private string _recommendationSourceUrlText = string.Empty;
        private string _loadoutStatusText = "No loadout has been calculated.";
        private string _recommendationStatusText = "No community recommendation data available.";
        private string _scrapeStatusText = "Community recommendations have not been refreshed.";
        private string _lastScrapeSummaryText = "No completed recommendation refresh has been recorded.";
        private string _rosterPlanStatusText = "No priority roster plan has been calculated.";
        private string _loadoutScoreText = "No assignment score has been calculated.";
        private string _projectedStatSummaryText = "Projected stat comparison is unavailable until current equipped mods are cached.";
        private bool _isRecommendationStale;
        private bool _isScraping;

        private static readonly TimeSpan RecommendationFreshness = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets the collection of available characters for optimization.
        /// </summary>
        public ObservableCollection<CharacterEntity> Characters { get; } = new();

        /// <summary>
        /// Gets the collection of optimal mods computed for the selected character.
        /// </summary>
        public ObservableCollection<GameModEntity> RecommendedLoadout { get; } = new();

        /// <summary>
        /// Gets the collection of target mod sets recommended by swgoh.gg.
        /// </summary>
        public ObservableCollection<string> TargetSets { get; } = new();

        /// <summary>
        /// Gets the collection of target primary stats per mod slot recommended by swgoh.gg.
        /// </summary>
        public ObservableCollection<string> TargetPrimaries { get; } = new();

        /// <summary>
        /// Gets the explanation for each selected mod in the calculated loadout.
        /// </summary>
        public ObservableCollection<string> LoadoutExplanations { get; } = new();

        /// <summary>
        /// Gets lower-ranked candidates that can be considered for individual slots.
        /// </summary>
        public ObservableCollection<string> AlternativeSummaries { get; } = new();

        /// <summary>
        /// Gets actionable replacement candidates for equipped mods.
        /// </summary>
        public ObservableCollection<string> SwapRecommendationSummaries { get; } = new();

        /// <summary>
        /// Gets projected persisted mod-stat changes between current equipped mods and the proposed loadout.
        /// </summary>
        public ObservableCollection<string> ProjectedStatImpactSummaries { get; } = new();

        public bool HasAlternatives => AlternativeSummaries.Count > 0;

        public bool HasSwapRecommendations => SwapRecommendationSummaries.Count > 0;

        public bool HasProjectedStatImpacts => ProjectedStatImpactSummaries.Count > 0;

        /// <summary>
        /// Gets the concise priority-roster assignment summaries.
        /// </summary>
        public ObservableCollection<string> RosterPlanSummaries { get; } = new();

        /// <summary>
        /// Gets roster-level swap candidates with inventory reservation context.
        /// </summary>
        public ObservableCollection<string> RosterSwapSummaries { get; } = new();

        public bool HasRosterSwapRecommendations => RosterSwapSummaries.Count > 0;

        /// <summary>
        /// Gets or sets the page header text.
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
        /// Gets or sets the selected character to optimize.
        /// </summary>
        public CharacterEntity? SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                if (_selectedCharacter != value)
                {
                    _selectedCharacter = value;
                    OnPropertyChanged(nameof(SelectedCharacter));
                    OnPropertyChanged(nameof(IsEmpty));
                    _ = LoadOptimalLoadoutAsync();
                }
            }
        }

        /// <summary>
        /// Gets or sets the text representation of recommendation popularity.
        /// </summary>
        public string PopularityText
        {
            get => _popularityText;
            set
            {
                if (_popularityText != value)
                {
                    _popularityText = value;
                    OnPropertyChanged(nameof(PopularityText));
                }
            }
        }

        /// <summary>
        /// Gets or sets the text representation of the last updated timestamp.
        /// </summary>
        public string LastUpdatedText
        {
            get => _lastUpdatedText;
            set
            {
                if (_lastUpdatedText != value)
                {
                    _lastUpdatedText = value;
                    OnPropertyChanged(nameof(LastUpdatedText));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the optimizer is currently calculating.
        /// </summary>
        public override bool IsBusy => State.Status == OperationStatus.Loading || IsScraping;

        public override bool IsEmpty => State.Status == OperationStatus.Empty ||
            (State.Status == OperationStatus.Success && SelectedCharacter == null);

        public bool HasLoadout => State.Status == OperationStatus.Success && RecommendedLoadout.Count > 0;


        protected override void OnStateChanged() =>
            OnPropertyChanged(nameof(HasLoadout));

        public bool IsScraping
        {
            get => _isScraping;
            private set
            {
                if (_isScraping == value)
                {
                    return;
                }

                _isScraping = value;
                OnPropertyChanged(nameof(IsScraping));
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        public string RecommendationSourceUrlText
        {
            get => _recommendationSourceUrlText;
            private set
            {
                if (_recommendationSourceUrlText == value)
                {
                    return;
                }

                _recommendationSourceUrlText = value;
                OnPropertyChanged(nameof(RecommendationSourceUrlText));
            }
        }

        public string LoadoutStatusText
        {
            get => _loadoutStatusText;
            private set
            {
                if (_loadoutStatusText == value)
                {
                    return;
                }

                _loadoutStatusText = value;
                OnPropertyChanged(nameof(LoadoutStatusText));
            }
        }

        public string RecommendationStatusText
        {
            get => _recommendationStatusText;
            private set
            {
                if (_recommendationStatusText == value)
                {
                    return;
                }

                _recommendationStatusText = value;
                OnPropertyChanged(nameof(RecommendationStatusText));
            }
        }

        public bool IsRecommendationStale
        {
            get => _isRecommendationStale;
            private set
            {
                if (_isRecommendationStale == value)
                {
                    return;
                }

                _isRecommendationStale = value;
                OnPropertyChanged(nameof(IsRecommendationStale));
            }
        }

        public string ScrapeStatusText
        {
            get => _scrapeStatusText;
            private set
            {
                if (_scrapeStatusText == value)
                {
                    return;
                }

                _scrapeStatusText = value;
                OnPropertyChanged(nameof(ScrapeStatusText));
            }
        }

        public string LastScrapeSummaryText
        {
            get => _lastScrapeSummaryText;
            private set
            {
                if (_lastScrapeSummaryText == value)
                {
                    return;
                }

                _lastScrapeSummaryText = value;
                OnPropertyChanged(nameof(LastScrapeSummaryText));
            }
        }

        public string RosterPlanStatusText
        {
            get => _rosterPlanStatusText;
            private set
            {
                if (_rosterPlanStatusText == value)
                {
                    return;
                }

                _rosterPlanStatusText = value;
                OnPropertyChanged(nameof(RosterPlanStatusText));
            }
        }

        public string LoadoutScoreText
        {
            get => _loadoutScoreText;
            private set
            {
                if (_loadoutScoreText == value)
                {
                    return;
                }

                _loadoutScoreText = value;
                OnPropertyChanged(nameof(LoadoutScoreText));
            }
        }

        public string ProjectedStatSummaryText
        {
            get => _projectedStatSummaryText;
            private set
            {
                if (_projectedStatSummaryText == value)
                {
                    return;
                }

                _projectedStatSummaryText = value;
                OnPropertyChanged(nameof(ProjectedStatSummaryText));
            }
        }

        public IAsyncRelayCommand ScrapeCommand { get; }

        public IAsyncRelayCommand RefreshCommand { get; }

        public IAsyncRelayCommand ScrapeAllCommand { get; }

        public IAsyncRelayCommand CalculateRosterCommand { get; }

        public IAsyncRelayCommand OptimizeRosterCommand { get; }

        public IRelayCommand CancelScrapeCommand { get; }

        public async Task RefreshAsync()
        {
            await LoadCharactersAsync().ConfigureAwait(true);
            if (SelectedCharacter != null)
            {
                await LoadOptimalLoadoutAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModOptimizerViewModel"/> class.
        /// </summary>
        public ModOptimizerViewModel(AppDbContext context, IModAssignmentService assignmentService)
            : this(context, assignmentService, null, null, null)
        {
        }

        public ModOptimizerViewModel(
            AppDbContext context,
            IModAssignmentService assignmentService,
            ISwgohGgScraperService? scraperService,
            Func<string?>? activeAllyCodeProvider = null,
            ISettingsService? settingsService = null)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(assignmentService);

            _context = context;
            _assignmentService = assignmentService;
            _scraperService = scraperService;
            _settingsService = settingsService;
            _activeAllyCodeProvider = activeAllyCodeProvider;
            LastScrapeSummaryText = FormatScrapeSummary(settingsService?.CurrentSettings.LastRecommendationScrape);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            ScrapeCommand = new AsyncRelayCommand(ScrapeSelectedAsync);
            ScrapeAllCommand = new AsyncRelayCommand(ScrapeAllAsync);
            CalculateRosterCommand = new AsyncRelayCommand(CalculateRosterAsync);
            OptimizeRosterCommand = new AsyncRelayCommand(CalculateOptimizedRosterAsync);
            CancelScrapeCommand = new RelayCommand(CancelScrape);
        }

        public Task CalculateRosterAsync() => CalculateRosterPlanAsync(globalOptimization: false);

        private Task CalculateOptimizedRosterAsync() => CalculateRosterPlanAsync(globalOptimization: true);

        private async Task CalculateRosterPlanAsync(bool globalOptimization)
        {
            RosterPlanSummaries.Clear();
            RosterSwapSummaries.Clear();
            OnPropertyChanged(nameof(HasRosterSwapRecommendations));
            RosterPlanStatusText = globalOptimization
                ? "Calculating a bounded global roster plan..."
                : "Calculating a priority-first roster plan...";
            try
            {
                var activeAllyCode = _activeAllyCodeProvider?.Invoke()?.Trim();
                var charactersQuery = _context.Characters.AsNoTracking();
                var modsQuery = _context.Mods.AsNoTracking();
                if (_activeAllyCodeProvider != null && string.IsNullOrWhiteSpace(activeAllyCode))
                {
                    charactersQuery = charactersQuery.Where(character => false);
                    modsQuery = modsQuery.Where(mod => false);
                }
                else if (!string.IsNullOrWhiteSpace(activeAllyCode))
                {
                    charactersQuery = charactersQuery.Where(character => character.PlayerAllyCode == activeAllyCode);
                    modsQuery = modsQuery.Where(mod => mod.PlayerAllyCode == activeAllyCode);
                }

                var characters = await charactersQuery.ToListAsync().ConfigureAwait(true);
                var inventory = await modsQuery.ToListAsync().ConfigureAwait(true);
                var plan = globalOptimization
                    ? await _assignmentService.CalculateGloballyOptimizedRosterLoadoutsAsync(
                        characters,
                        inventory).ConfigureAwait(true)
                    : await _assignmentService.CalculateRosterLoadoutsAsync(
                        characters,
                        inventory).ConfigureAwait(true);

                foreach (var characterPlan in plan.Plans)
                {
                    RosterPlanSummaries.Add(
                        $"P{characterPlan.Priority} {characterPlan.CharacterName}: " +
                        $"{characterPlan.Loadout.Mods.Count}/6 mods — {characterPlan.Loadout.Status}");
                    foreach (var conflict in characterPlan.Conflicts)
                    {
                        RosterPlanSummaries.Add(
                            $"  Slot {conflict.Slot} conflict for {conflict.CharacterName}: {conflict.Reason}");
                    }
                }

                foreach (var swap in plan.SwapRecommendations)
                {
                    var availability = swap.CandidateAvailable ? "available" : "reserved/unavailable";
                    RosterSwapSummaries.Add(
                        $"P{swap.Priority} {swap.CharacterName}, slot {swap.Slot}: " +
                        $"{swap.CurrentModId} -> {swap.CandidateModId} (+{swap.ScoreGain:F1}, {availability}) — {swap.Reason}");
                }
                OnPropertyChanged(nameof(HasRosterSwapRecommendations));

                RosterPlanStatusText = plan.SwapRecommendations.Count == 0
                    ? plan.Status
                    : $"{plan.Status} {plan.SwapRecommendations.Count} roster swap candidate(s) consolidated below.";
            }
            catch (Exception ex)
            {
                RosterPlanStatusText = $"Roster planning failed: {ex.Message}";
            }
        }

        private async Task ScrapeSelectedAsync()
        {
            if (_settingsService?.CurrentSettings.EnableLocalRecommendationScraping == false)
            {
                ScrapeStatusText = "Local recommendation scraping is disabled in Settings. Enable it before refreshing community data.";
                return;
            }

            if (_scraperService == null || SelectedCharacter == null)
            {
                ScrapeStatusText = "Select a character and configure the scraper before updating recommendations.";
                return;
            }

            _scrapeCancellation?.Dispose();
            _scrapeCancellation = new CancellationTokenSource();
            IsScraping = true;
            ScrapeStatusText = $"Refreshing recommendations for {SelectedCharacter.Name}...";
            var scrapeSucceeded = false;
            var scrapeFailed = false;
            var cancelled = false;

            try
            {
                var scrapeResult = await _scraperService.ScrapeCharacterRecommendationsWithResultAsync(
                    SelectedCharacter.Id,
                    _scrapeCancellation.Token,
                    _activeAllyCodeProvider?.Invoke()?.Trim());
                scrapeSucceeded = scrapeResult.Success;
                scrapeFailed = !scrapeSucceeded;
                ScrapeStatusText = scrapeSucceeded
                    ? "Recommendation data refreshed."
                    : scrapeResult.ErrorMessage ?? "No recommendation data was returned for this character.";

                if (scrapeSucceeded)
                {
                    await LoadOptimalLoadoutAsync();
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                ScrapeStatusText = "Recommendation refresh cancelled.";
            }
            catch (Exception ex)
            {
                scrapeFailed = true;
                ScrapeStatusText = $"Recommendation refresh failed: {ex.Message}";
            }
            finally
            {
                await SaveScrapeSummaryAsync(1, scrapeSucceeded ? 1 : 0, scrapeFailed ? 1 : 0, cancelled);
                IsScraping = false;
                _scrapeCancellation?.Dispose();
                _scrapeCancellation = null;
            }
        }

        private void CancelScrape()
        {
            _scrapeCancellation?.Cancel();
        }

        private async Task ScrapeAllAsync()
        {
            if (_settingsService?.CurrentSettings.EnableLocalRecommendationScraping == false)
            {
                ScrapeStatusText = "Local recommendation scraping is disabled in Settings. Enable it before refreshing community data.";
                return;
            }

            if (_scraperService == null)
            {
                ScrapeStatusText = "The recommendation scraper is not configured.";
                return;
            }

            _scrapeCancellation?.Dispose();
            _scrapeCancellation = new CancellationTokenSource();
            IsScraping = true;
            ScrapeStatusText = "Preparing incremental recommendation refresh...";
            var processed = 0;
            var succeeded = 0;
            var failed = 0;
            var cancelled = false;

            try
            {
                var progress = new Progress<ScrapeProgress>(update =>
                {
                    processed = update.Current;
                    if (update.Success)
                    {
                        succeeded++;
                    }
                    else
                    {
                        failed++;
                    }

                    ScrapeStatusText = update.Success
                        ? $"Refreshed {update.Current}/{update.Total}: {update.CurrentCharacterName}"
                        : $"Failed {update.Current}/{update.Total}: {update.CurrentCharacterName} — {update.ErrorMessage}";
                });

                await _scraperService.ScrapeAllCharactersIncrementalAsync(
                    progress,
                    _scrapeCancellation.Token,
                    _activeAllyCodeProvider?.Invoke()?.Trim());

                ScrapeStatusText = "Incremental recommendation refresh completed.";
                await LoadCharactersAsync();
                if (SelectedCharacter != null)
                {
                    await LoadOptimalLoadoutAsync();
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                ScrapeStatusText = "Incremental recommendation refresh cancelled.";
            }
            catch (Exception ex)
            {
                failed++;
                ScrapeStatusText = $"Incremental recommendation refresh failed: {ex.Message}";
            }
            finally
            {
                await SaveScrapeSummaryAsync(processed, succeeded, failed, cancelled);
                IsScraping = false;
                _scrapeCancellation?.Dispose();
                _scrapeCancellation = null;
            }
        }

        private async Task SaveScrapeSummaryAsync(
            int processed,
            int succeeded,
            int failed,
            bool cancelled)
        {
            var summary = new RecommendationScrapeSummary(
                DateTime.UtcNow,
                processed,
                succeeded,
                failed,
                cancelled);
            LastScrapeSummaryText = FormatScrapeSummary(summary);

            if (_settingsService == null)
            {
                return;
            }

            try
            {
                await _settingsService.SaveSettingsAsync(
                    _settingsService.CurrentSettings with
                    {
                        LastRecommendationScrape = summary
                    });
            }
            catch (Exception ex)
            {
                ScrapeStatusText = $"Refresh completed, but its summary could not be saved: {ex.Message}";
            }
        }

        private static string FormatScrapeSummary(RecommendationScrapeSummary? summary)
        {
            if (summary == null)
            {
                return "No completed recommendation refresh has been recorded.";
            }

            var state = summary.Cancelled ? "cancelled" : "completed";
            return $"Last refresh {state} {summary.CompletedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}: " +
                $"{summary.Processed} processed, {summary.Succeeded} succeeded, {summary.Failed} failed.";
        }

        public async Task LoadCharactersAsync()
        {
            try
            {
                var activeAllyCode = _activeAllyCodeProvider?.Invoke()?.Trim();
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
                    .OrderByDescending(c => c.Priority)
                    .ThenBy(c => c.Name)
                    .ToListAsync()
                    .ConfigureAwait(true);
                var selectedCharacterId = SelectedCharacter?.Id;

                Characters.Clear();
                foreach (var character in characters)
                {
                    Characters.Add(character);
                }

                SelectedCharacter = string.IsNullOrWhiteSpace(selectedCharacterId)
                    ? null
                    : Characters.FirstOrDefault(character =>
                        string.Equals(character.Id, selectedCharacterId, StringComparison.Ordinal));

                State = Characters.Count == 0
                    ? OperationState<IReadOnlyList<GameModEntity>>.ToEmpty()
                    : OperationState<IReadOnlyList<GameModEntity>>.ToSuccess(Array.Empty<GameModEntity>());
            }
            catch (Exception ex)
            {
                State = OperationState<IReadOnlyList<GameModEntity>>.ToError(
                    $"Failed to load optimizer characters: {ex.Message}");
            }
        }

        private async Task LoadOptimalLoadoutAsync()
        {
            if (SelectedCharacter == null)
            {
                RecommendedLoadout.Clear();
                TargetSets.Clear();
                TargetPrimaries.Clear();
                LoadoutExplanations.Clear();
                AlternativeSummaries.Clear();
                SwapRecommendationSummaries.Clear();
                ProjectedStatImpactSummaries.Clear();
                OnPropertyChanged(nameof(HasAlternatives));
                OnPropertyChanged(nameof(HasSwapRecommendations));
                OnPropertyChanged(nameof(HasProjectedStatImpacts));
                ProjectedStatSummaryText = "Projected stat comparison is unavailable until current equipped mods are cached.";
                PopularityText = "No community recommendation data available.";
                LastUpdatedText = string.Empty;
                RecommendationSourceUrlText = string.Empty;
                LoadoutStatusText = "Select a character to calculate a loadout.";
                RecommendationStatusText = "No community recommendation data available.";
                IsRecommendationStale = false;
                State = OperationState<IReadOnlyList<GameModEntity>>.ToEmpty();
                return;
            }

            State = OperationState<IReadOnlyList<GameModEntity>>.ToLoading();
            var characterId = SelectedCharacter.Id;

            try
            {
                // Fetch scraped community insights from SQLite cache
                var activeAllyCode = _activeAllyCodeProvider?.Invoke()?.Trim() ?? string.Empty;
                var recommendation = await _context.SwgohGgRecommendations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => r.CharacterId == characterId && r.PlayerAllyCode == activeAllyCode)
                    .ConfigureAwait(true);

                TargetSets.Clear();
                TargetPrimaries.Clear();
                LoadoutExplanations.Clear();
                AlternativeSummaries.Clear();
                SwapRecommendationSummaries.Clear();
                ProjectedStatImpactSummaries.Clear();
                OnPropertyChanged(nameof(HasAlternatives));
                OnPropertyChanged(nameof(HasSwapRecommendations));
                OnPropertyChanged(nameof(HasProjectedStatImpacts));
                ProjectedStatSummaryText = "Projected stat comparison is unavailable until current equipped mods are cached.";
                PopularityText = "No community recommendation data available.";
                LastUpdatedText = string.Empty;
                RecommendationSourceUrlText = string.Empty;
                LoadoutStatusText = "Calculating a loadout from the cached inventory.";
                RecommendationStatusText = "No community recommendation data available.";
                IsRecommendationStale = false;

                if (recommendation != null)
                {
                    IsRecommendationStale = DateTime.UtcNow - recommendation.LastUpdatedUtc >= RecommendationFreshness;
                    RecommendationStatusText = IsRecommendationStale
                        ? "Stale community data. Refresh recommendations before relying on this loadout."
                        : "Community recommendation data is current.";
                    PopularityText = $"Community Popularity: {recommendation.PopularityPercentage:F1}%";
                    LastUpdatedText = $"Scraped: {recommendation.LastUpdatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

                    try
                    {
                        var snapshot = RecommendationSnapshot.FromEntity(recommendation);
                        var source = string.IsNullOrWhiteSpace(snapshot.Source)
                            ? "community"
                            : snapshot.Source;
                        IsRecommendationStale = DateTime.UtcNow - snapshot.ScrapedAtUtc >= RecommendationFreshness;
                        RecommendationStatusText = IsRecommendationStale
                            ? $"Stale {source} data. Refresh recommendations before relying on this loadout."
                            : $"Current {source} recommendation data.";
                        PopularityText = $"Community Popularity: {snapshot.PopularityPercentage:F1}%";
                        LastUpdatedText = $"Scraped: {snapshot.ScrapedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
                        RecommendationSourceUrlText = string.IsNullOrWhiteSpace(snapshot.SourceUrl)
                            ? "Source URL unavailable."
                            : $"Source: {snapshot.SourceUrl}";

                        foreach (var set in snapshot.Sets)
                        {
                            TargetSets.Add($"{set.Name} ({set.Percentage:F1}%)");
                        }

                        foreach (var kvp in snapshot.PrimaryStats)
                        {
                            var values = string.Join(", ", kvp.Value.Select(primary =>
                                $"{primary.StatName} ({primary.Percentage:F1}%)"));
                            TargetPrimaries.Add($"{kvp.Key}: {values}");
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to deserialize recommendation payload: {ex.Message}");
                        RecommendationStatusText = "Cached recommendation data is malformed. Refresh recommendations.";
                    }
                }

                // Offload CPU heavy work and Db Queries to background thread (Rule 9)
                var loadoutResult = await Task.Run(async () =>
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

                    var availableMods = await modsQuery
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (availableMods.Count == 0)
                    {
                        return new ModLoadoutResult(
                            Array.Empty<GameModEntity>(),
                            recommendation != null,
                            false,
                            false,
                            "No inventory mods are available for the active account.",
                            Array.Empty<ModAssignmentExplanation>());
                    }

                    return await _assignmentService.CalculateOptimalLoadoutResultAsync(
                        characterId,
                        availableMods).ConfigureAwait(false);
                });

                // Safely update state back on the UI thread
                RecommendedLoadout.Clear();
                foreach (var mod in loadoutResult.Mods)
                {
                    RecommendedLoadout.Add(mod);
                }
                foreach (var explanation in loadoutResult.Explanations)
                {
                    LoadoutExplanations.Add($"Slot {explanation.Slot} - {explanation.ModId}: {explanation.Reason}");
                }
                LoadoutScoreText = loadoutResult.Explanations.Count == 0
                    ? "No assignment score is available for this loadout."
                    : $"Combined assignment score: {loadoutResult.Explanations.Sum(explanation => explanation.Score):F1}. " +
                      "Score compares persisted mod quality with recommended sets, primaries, and secondary-stat quality; it is not a guaranteed in-game stat gain.";
                foreach (var alternative in loadoutResult.Alternatives)
                {
                    AlternativeSummaries.Add(
                        $"Slot {alternative.Slot} - {alternative.ModId}: score {alternative.Score:F1} " +
                        $"({alternative.ScoreDelta:+0.0;-0.0;0.0} vs selected) - {alternative.BenefitSummary} {alternative.Reason}");
                }
                foreach (var swap in loadoutResult.SwapRecommendations)
                {
                    SwapRecommendationSummaries.Add(
                        $"Replace {swap.CurrentModId} with {swap.CandidateModId} in slot {swap.Slot}: " +
                      $"{swap.BenefitSummary} {swap.Reason}");
                }
                foreach (var impact in loadoutResult.Projection.StatImpacts)
                {
                    ProjectedStatImpactSummaries.Add(impact.Summary);
                }
                ProjectedStatSummaryText = loadoutResult.Projection.HasCurrentEquippedMods
                    ? loadoutResult.Projection.HasChanges
                        ? loadoutResult.Projection.Disclaimer
                        : "The proposed loadout does not change the persisted mod-stat totals from the current equipped set."
                    : "Projected stat comparison is unavailable because no current equipped mods were found for this account.";
                OnPropertyChanged(nameof(HasProjectedStatImpacts));
                OnPropertyChanged(nameof(HasAlternatives));
                OnPropertyChanged(nameof(HasSwapRecommendations));
                LoadoutStatusText = loadoutResult.Status;
                OnPropertyChanged(nameof(HasLoadout));
                OnPropertyChanged(nameof(IsEmpty));
                State = loadoutResult.Mods.Count == 0
                    ? OperationState<IReadOnlyList<GameModEntity>>.ToEmpty()
                    : OperationState<IReadOnlyList<GameModEntity>>.ToSuccess(loadoutResult.Mods);
            }
            catch (Exception ex)
            {
                State = OperationState<IReadOnlyList<GameModEntity>>.ToError(
                    $"Failed to calculate loadout: {ex.Message}");
            }
        }
    }
}
