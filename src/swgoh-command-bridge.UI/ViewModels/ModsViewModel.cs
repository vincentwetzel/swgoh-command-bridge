#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing the mod selection, upgrading, and overall management page.
    /// </summary>
    public class ModsViewModel : ViewModelBase
    {
        private string _headerText = "Mod Management & Upgrades";
        private GameModEntity? _selectedMod;
        private ModRecommendation? _selectedModRecommendation;
        private int _rarityFilter;
        private string _sortOption = "Rarity";
        private readonly List<GameModEntity> _allMods = new();
        private readonly IModAdvisorService _advisorService;

        /// <summary>
        /// Gets the collection of filtered mods displayed in the grid.
        /// </summary>
        public ObservableCollection<GameModEntity> FilteredMods { get; } = new();

        /// <summary>
        /// Gets or sets the header text for the mods panel.
        /// </summary>
        public string HeaderText
        {
            get => _headerText;
            set
            {
                if (_headerText != value)
                {
                    _headerText = value;
                    // Assumes ViewModelBase implements standard property change notifications
                    OnPropertyChanged(nameof(HeaderText));
                }
            }
        }

        /// <summary>
        /// Gets or sets the currently selected mod.
        /// </summary>
        public GameModEntity? SelectedMod
        {
            get => _selectedMod;
            set
            {
                if (_selectedMod != value)
                {
                    _selectedMod = value;
                    OnPropertyChanged(nameof(SelectedMod));
                    _ = UpdateRecommendationAsync();
                }
            }
        }

        /// <summary>
        /// Gets or sets the recommendation response details for the selected mod.
        /// </summary>
        public ModRecommendation? SelectedModRecommendation
        {
            get => _selectedModRecommendation;
            set
            {
                if (_selectedModRecommendation != value)
                {
                    _selectedModRecommendation = value;
                    OnPropertyChanged(nameof(SelectedModRecommendation));
                }
            }
        }

        /// <summary>
        /// Gets or sets the sorting criteria option.
        /// </summary>
        public string SortOption
        {
            get => _sortOption;
            set
            {
                if (_sortOption != value)
                {
                    _sortOption = value;
                    OnPropertyChanged(nameof(SortOption));
                    ApplyFiltersAndSort();
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum rarity filter.
        /// </summary>
        public int RarityFilter
        {
            get => _rarityFilter;
            set
            {
                if (_rarityFilter != value)
                {
                    _rarityFilter = value;
                    OnPropertyChanged(nameof(RarityFilter));
                    ApplyFiltersAndSort();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModsViewModel"/> class.
        /// </summary>
        public ModsViewModel(IModAdvisorService advisorService)
        {
            ArgumentNullException.ThrowIfNull(advisorService);
            _advisorService = advisorService;

            // Populate sample data for UI demonstration and preview purposes
            _allMods.Add(new GameModEntity { Id = "1", Rarity = 5, Tier = 5, Slot = 1, Level = 15, CharacterId = "Luke Skywalker" });
            _allMods.Add(new GameModEntity { Id = "2", Rarity = 6, Tier = 1, Slot = 2, Level = 15, CharacterId = "Darth Vader" });
            _allMods.Add(new GameModEntity { Id = "3", Rarity = 4, Tier = 3, Slot = 1, Level = 12, CharacterId = "Han Solo" });

            ApplyFiltersAndSort();
        }

        private void ApplyFiltersAndSort()
        {
            var query = _allMods.AsEnumerable();

            if (_rarityFilter > 0)
            {
                query = query.Where(m => m.Rarity == _rarityFilter);
            }

            query = _sortOption switch
            {
                "Level" => query.OrderByDescending(m => m.Level),
                "Rarity" => query.OrderByDescending(m => m.Rarity),
                "Tier" => query.OrderByDescending(m => m.Tier),
                _ => query.OrderByDescending(m => m.Rarity)
            };

            FilteredMods.Clear();
            foreach (var mod in query)
            {
                FilteredMods.Add(mod);
            }
        }

        private async Task UpdateRecommendationAsync()
        {
            if (SelectedMod == null)
            {
                SelectedModRecommendation = null;
                return;
            }

            // Create standard threshold parameters for evaluation
            var defaultThreshold = new ModUpgradeThreshold(
                Id: "default",
                Name: "Standard Settings",
                MinimumRarity: 5,
                MinimumTier: 4,
                MinimumSpeed: 10,
                UpgradeOnlyWithSpeed: true,
                MinimumEfficiency: 60.0
            );

            SelectedModRecommendation = await _advisorService.AnalyzeModAsync(SelectedMod, defaultThreshold).ConfigureAwait(true);
        }
    }
}