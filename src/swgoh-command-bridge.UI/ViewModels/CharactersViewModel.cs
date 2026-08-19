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
        private bool _catalogRepairAttempted;
        private bool _catalogRepairInProgress;
        private string _headerText = "Characters List";
        private string _searchText = string.Empty;
        private string _catalogStatusText = string.Empty;
        /// <summary>
        /// Gets the collection of characters loaded from the database.
        /// </summary>
        public ObservableCollection<CharacterEntity> Characters { get; } = new();

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
            DiagnosticEventLog? eventLog = null)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _activeAllyCodeProvider = activeAllyCodeProvider;
            _characterCatalogService = characterCatalogService;
            _eventLog = eventLog;
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

                Characters.Clear();
                foreach (var character in list)
                {
                    Characters.Add(character);
                }

                if (Characters.Count == 0)
                {
                    State = OperationState<IReadOnlyList<CharacterEntity>>.ToEmpty();
                }
                else
                {
                    State = OperationState<IReadOnlyList<CharacterEntity>>.ToSuccess(list);
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

    }
}
