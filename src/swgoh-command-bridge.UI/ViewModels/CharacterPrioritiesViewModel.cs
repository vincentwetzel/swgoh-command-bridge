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
    /// ViewModel representing the workspace for defining individual priority tiers across unlocked roster units.
    /// </summary>
    public class CharacterPrioritiesViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private string _headerText = "Configure Character Priorities";
        private CharacterEntity? _selectedCharacter;
        private int _selectedCharacterPriority;
        private bool _isBusy;

        /// <summary>
        /// Gets the collection of characters available to update priorities.
        /// </summary>
        public ObservableCollection<CharacterEntity> Characters { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterPrioritiesViewModel"/> class.
        /// </summary>
        public CharacterPrioritiesViewModel(AppDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _ = LoadCharactersAsync();
        }

        /// <summary>
        /// Gets or sets the header text.
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
        /// Gets or sets the currently selected character for priority adjustment.
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

                    if (_selectedCharacter != null)
                    {
                        SelectedCharacterPriority = _selectedCharacter.Priority;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the target priority score of the selected character.
        /// </summary>
        public int SelectedCharacterPriority
        {
            get => _selectedCharacterPriority;
            set
            {
                if (_selectedCharacterPriority != value)
                {
                    _selectedCharacterPriority = value;
                    OnPropertyChanged(nameof(SelectedCharacterPriority));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether an asynchronous operation is in progress.
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
        /// Loads the character entries asynchronously to build the selection pool.
        /// </summary>
        public async Task LoadCharactersAsync()
        {
            IsBusy = true;
            try
            {
                var list = await _context.Characters
                    .AsNoTracking()
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
                    Characters.Add(new CharacterEntity { Id = "luke_skyw_v2", Name = "Commander Luke Skywalker", Priority = 100 });
                    Characters.Add(new CharacterEntity { Id = "darth_vader", Name = "Darth Vader", Priority = 90 });
                }

                SelectedCharacter = Characters.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading character priorities: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Saves the modified priority of the selected character back to the local database cache.
        /// </summary>
        public async Task SavePriorityAsync()
        {
            if (SelectedCharacter == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var character = await _context.Characters
                    .FirstOrDefaultAsync(c => c.Id == SelectedCharacter.Id && c.PlayerAllyCode == SelectedCharacter.PlayerAllyCode)
                    .ConfigureAwait(true);

                if (character != null)
                {
                    character.Priority = _selectedCharacterPriority;
                    await _context.SaveChangesAsync().ConfigureAwait(true);
                }
                else
                {
                    SelectedCharacter.Priority = _selectedCharacterPriority;
                }

                await LoadCharactersAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving character priority: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}