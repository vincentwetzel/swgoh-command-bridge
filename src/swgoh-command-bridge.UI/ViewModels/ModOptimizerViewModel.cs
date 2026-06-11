#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Services;

namespace swgoh_command_bridge.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing the mod optimization and recommendation interface.
    /// </summary>
    public class ModOptimizerViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private readonly IModAssignmentService _assignmentService;
        private string _headerText = "Mod Assignment Optimizer";
        private CharacterEntity? _selectedCharacter;
        private bool _isBusy;
        private string _popularityText = "No community recommendation data available.";
        private string _lastUpdatedText = string.Empty;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

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
        /// Initializes a new instance of the <see cref="ModOptimizerViewModel"/> class.
        /// </summary>
        public ModOptimizerViewModel(AppDbContext context, IModAssignmentService assignmentService)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(assignmentService);

            _context = context;
            _assignmentService = assignmentService;

            _ = InitializeCharactersAsync();
        }

        private async Task InitializeCharactersAsync()
        {
            try
            {
                var characters = await _context.Characters
                    .AsNoTracking()
                    .OrderByDescending(c => c.Priority)
                    .ThenBy(c => c.Name)
                    .ToListAsync()
                    .ConfigureAwait(true);

                Characters.Clear();
                foreach (var character in characters)
                {
                    Characters.Add(character);
                }

                // Setup fallback/mock if empty for UX preview
                if (Characters.Count == 0)
                {
                    Characters.Add(new CharacterEntity { Id = "luke_skyw_v2", Name = "Commander Luke Skywalker", Priority = 100 });
                    Characters.Add(new CharacterEntity { Id = "darth_vader", Name = "Darth Vader", Priority = 90 });
                }
            }
            catch
            {
                // Graceful fallback
            }
        }

        private async Task LoadOptimalLoadoutAsync()
        {
            if (SelectedCharacter == null)
            {
                RecommendedLoadout.Clear();
                TargetSets.Clear();
                TargetPrimaries.Clear();
                PopularityText = "No community recommendation data available.";
                LastUpdatedText = string.Empty;
                return;
            }

            IsBusy = true;
            var characterId = SelectedCharacter.Id;

            try
            {
                // Fetch scraped community insights from SQLite cache
                var recommendation = await _context.SwgohGgRecommendations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.CharacterId == characterId)
                    .ConfigureAwait(true);

                TargetSets.Clear();
                TargetPrimaries.Clear();
                PopularityText = "No community recommendation data available.";
                LastUpdatedText = string.Empty;

                if (recommendation != null)
                {
                    PopularityText = $"Community Popularity: {recommendation.PopularityPercentage:F1}%";
                    LastUpdatedText = $"Scraped: {recommendation.LastUpdatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

                    try
                    {
                        var sets = JsonSerializer.Deserialize<List<string>>(recommendation.SetRecommendationsJson, SerializerOptions);
                        if (sets != null)
                        {
                            foreach (var set in sets)
                            {
                                TargetSets.Add(set);
                            }
                        }

                        var primaries = JsonSerializer.Deserialize<Dictionary<string, string>>(recommendation.PrimaryStatsJson, SerializerOptions);
                        if (primaries != null)
                        {
                            foreach (var kvp in primaries)
                            {
                                TargetPrimaries.Add($"{kvp.Key}: {kvp.Value}");
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to deserialize recommendation payload: {ex.Message}");
                    }
                }

                // Offload CPU heavy work and Db Queries to background thread (Rule 9)
                var optimalMods = await Task.Run(async () =>
                {
                    var availableMods = await _context.Mods
                        .AsNoTracking()
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (availableMods.Count == 0)
                    {
                        availableMods = new List<GameModEntity>
                        {
                            new GameModEntity { Id = "m1", Slot = 1, Rarity = 6, Level = 15, Tier = 5, Set = 6 },
                            new GameModEntity { Id = "m2", Slot = 2, Rarity = 5, Level = 15, Tier = 5, Set = 6 },
                            new GameModEntity { Id = "m3", Slot = 3, Rarity = 5, Level = 15, Tier = 1, Set = 6 },
                            new GameModEntity { Id = "m4", Slot = 4, Rarity = 6, Level = 15, Tier = 5, Set = 1 },
                            new GameModEntity { Id = "m5", Slot = 5, Rarity = 5, Level = 15, Tier = 4, Set = 1 },
                            new GameModEntity { Id = "m6", Slot = 6, Rarity = 5, Level = 15, Tier = 5, Set = 6 }
                        };
                    }

                    return await _assignmentService.CalculateOptimalLoadoutAsync(characterId, availableMods).ConfigureAwait(false);
                });

                // Safely update state back on the UI thread
                RecommendedLoadout.Clear();
                foreach (var mod in optimalMods)
                {
                    RecommendedLoadout.Add(mod);
                }
            }
            catch
            {
                // Graceful error handling
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}