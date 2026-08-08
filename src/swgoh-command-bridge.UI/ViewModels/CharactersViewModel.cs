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

namespace swgoh_command_bridge.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing the character collection and priority scoring page.
    /// </summary>
    public class CharactersViewModel : StateViewModelBase<IReadOnlyList<CharacterEntity>>
    {
        private readonly AppDbContext _context;
        private readonly Func<string?>? _activeAllyCodeProvider;
        private string _headerText = "Characters List";
        private string _searchText = string.Empty;
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
        /// Initializes a new instance of the <see cref="CharactersViewModel"/> class.
        /// </summary>
        public CharactersViewModel(AppDbContext context)
            : this(context, null)
        {
        }

        public CharactersViewModel(AppDbContext context, Func<string?>? activeAllyCodeProvider)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _activeAllyCodeProvider = activeAllyCodeProvider;
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
    }
}
