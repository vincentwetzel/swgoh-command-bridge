#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;

namespace swgoh_command_bridge.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing the character collection and priority scoring page.
    /// </summary>
    public class CharactersViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private string _headerText = "Characters List";
        private string _searchText = string.Empty;
        private bool _isBusy;

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
        /// Gets or sets a value indicating whether data is currently being retrieved.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged(nameof(IsBusy));
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CharactersViewModel"/> class.
        /// </summary>
        public CharactersViewModel()
            : this(new AppDbContext())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CharactersViewModel"/> class.
        /// </summary>
        public CharactersViewModel(AppDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _ = LoadCharactersAsync();
        }

        /// <summary>
        /// Asynchronously retrieves character lists matching the search filter criteria.
        /// </summary>
        public async Task LoadCharactersAsync()
        {
            IsBusy = true;
            try
            {
                var query = _context.Characters.AsNoTracking();

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

                if (Characters.Count == 0 && string.IsNullOrWhiteSpace(_searchText))
                {
                    Characters.Add(new CharacterEntity { Id = "luke_skyw_v2", Name = "Commander Luke Skywalker", Priority = 100 });
                    Characters.Add(new CharacterEntity { Id = "darth_vader", Name = "Darth Vader", Priority = 90 });
                    Characters.Add(new CharacterEntity { Id = "han_solo", Name = "Han Solo", Priority = 85 });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading characters: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
