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
    /// ViewModel representing the workspace for defining individual priority tiers across unlocked roster units.
    /// </summary>
    public class CharacterPrioritiesViewModel : StateViewModelBase<IReadOnlyList<CharacterEntity>>
    {
        private readonly AppDbContext _context;
        private readonly Func<string?>? _activeAllyCodeProvider;
        private string _headerText = "Configure Character Priorities";
        private CharacterEntity? _selectedCharacter;
        private int _selectedCharacterPriority;
        private int _originalPriority;
        private string _validationError = string.Empty;
        /// <summary>
        /// Gets the collection of characters available to update priorities.
        /// </summary>
        public ObservableCollection<CharacterEntity> Characters { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterPrioritiesViewModel"/> class.
        /// </summary>
        public CharacterPrioritiesViewModel(AppDbContext context)
            : this(context, null)
        {
        }

        public CharacterPrioritiesViewModel(AppDbContext context, Func<string?>? activeAllyCodeProvider)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _activeAllyCodeProvider = activeAllyCodeProvider;
            RefreshCommand = new AsyncRelayCommand(LoadCharactersAsync);
            SavePriorityCommand = new AsyncRelayCommand(SavePriorityAsync);
            CancelEditCommand = new RelayCommand(CancelEdit);
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
                    OnPropertyChanged(nameof(IsDirty));
                    OnPropertyChanged(nameof(CanSavePriority));

                    if (_selectedCharacter != null)
                    {
                        _originalPriority = _selectedCharacter.Priority;
                        SelectedCharacterPriority = _selectedCharacter.Priority;
                    }

                    ValidationError = string.Empty;
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
                    OnPropertyChanged(nameof(IsDirty));
                    OnPropertyChanged(nameof(CanSavePriority));
                }
            }
        }

        public bool IsDirty =>
            SelectedCharacter != null && SelectedCharacterPriority != _originalPriority;

        public bool CanSavePriority => IsDirty && !HasValidationError;

        public string ValidationError
        {
            get => _validationError;
            private set
            {
                if (_validationError == value)
                {
                    return;
                }

                _validationError = value;
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(HasValidationError));
                OnPropertyChanged(nameof(CanSavePriority));
            }
        }

        public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

        /// <summary>
        /// Gets a value indicating whether an asynchronous operation is in progress.
        /// </summary>
        public bool HasCharacters => State.Status == OperationStatus.Success;

        protected override void OnStateChanged() =>
            OnPropertyChanged(nameof(HasCharacters));

        public IAsyncRelayCommand RefreshCommand { get; }

        public IAsyncRelayCommand SavePriorityCommand { get; }

        public IRelayCommand CancelEditCommand { get; }

        /// <summary>
        /// Loads the character entries asynchronously to build the selection pool.
        /// </summary>
        public async Task LoadCharactersAsync()
        {
            State = OperationState<IReadOnlyList<CharacterEntity>>.ToLoading();
            try
            {
                var selectedCharacterId = SelectedCharacter?.Id;
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
                    SelectedCharacter = null;
                }
                else
                {
                    State = OperationState<IReadOnlyList<CharacterEntity>>.ToSuccess(list);
                    SelectedCharacter = Characters.FirstOrDefault(character =>
                        string.Equals(character.Id, selectedCharacterId, StringComparison.Ordinal))
                        ?? Characters.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading character priorities: {ex.Message}");
                State = OperationState<IReadOnlyList<CharacterEntity>>.ToError($"Failed to load priorities: {ex.Message}");
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

            ValidationError = ValidatePriority();
            if (HasValidationError)
            {
                return;
            }

            State = OperationState<IReadOnlyList<CharacterEntity>>.ToLoading();
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
                State = OperationState<IReadOnlyList<CharacterEntity>>.ToError($"Failed to save priority: {ex.Message}");
            }
        }

        private void CancelEdit()
        {
            if (SelectedCharacter == null)
            {
                return;
            }

            SelectedCharacterPriority = _originalPriority;
            ValidationError = string.Empty;
        }

        private string ValidatePriority() => _selectedCharacterPriority is < 0 or > 100
            ? "Priority must be between 0 and 100."
            : string.Empty;
    }
}
