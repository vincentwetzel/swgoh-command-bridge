# Details

Date : 2026-06-11 05:23:29

Directory e:\\coding_workspaces\\CPP\\swgoh-command-bridge

Total : 85 files,  3664 codes, 544 comments, 716 blanks, all 4924 lines

[Summary](results.md) / Details / [Diff Summary](diff.md) / [Diff Details](diff-details.md)

## Files
| filename | language | code | comment | blank | total |
| :--- | :--- | ---: | ---: | ---: | ---: |
| [CHANGELOG.md](/CHANGELOG.md) | Markdown | 33 | 0 | 6 | 39 |
| [CODING\_STANDARDS.md](/CODING_STANDARDS.md) | Markdown | 151 | 0 | 30 | 181 |
| [CharacterEntity.cs](/CharacterEntity.cs) | C# | 16 | 3 | 9 | 28 |
| [ComlinkService.cs](/ComlinkService.cs) | C# | 63 | 8 | 12 | 83 |
| [ComlinkService.hpp](/ComlinkService.hpp) | C++ | 14 | 3 | 7 | 24 |
| [GameModEntity.cs](/GameModEntity.cs) | C# | 16 | 3 | 9 | 28 |
| [IComlinkService.cs](/IComlinkService.cs) | C# | 11 | 9 | 3 | 23 |
| [IModAssignmentService.cs](/IModAssignmentService.cs) | C# | 15 | 6 | 2 | 23 |
| [IPlayerRepository.cs](/IPlayerRepository.cs) | C# | 12 | 9 | 3 | 24 |
| [ISwgohGgScraperService.cs](/ISwgohGgScraperService.cs) | C# | 12 | 9 | 3 | 24 |
| [ModAssignmentService.cs](/ModAssignmentService.cs) | C# | 110 | 12 | 22 | 144 |
| [PlayerEntity.cs](/PlayerEntity.cs) | C# | 14 | 3 | 7 | 24 |
| [PlayerRepository.cs](/PlayerRepository.cs) | C# | 64 | 8 | 14 | 86 |
| [README.md](/README.md) | Markdown | 29 | 0 | 9 | 38 |
| [SwgohGgRecommendationEntity.cs](/SwgohGgRecommendationEntity.cs) | C# | 13 | 18 | 6 | 37 |
| [SwgohGgScraperService.cs](/SwgohGgScraperService.cs) | C# | 114 | 14 | 24 | 152 |
| [TODO.md](/TODO.md) | Markdown | 64 | 0 | 9 | 73 |
| [docs/AGENTS.md](/docs/AGENTS.md) | Markdown | 14 | 0 | 7 | 21 |
| [docs/ARCHITECTURE.md](/docs/ARCHITECTURE.md) | Markdown | 41 | 0 | 12 | 53 |
| [docs/COMLINK\_SETUP.md](/docs/COMLINK_SETUP.md) | Markdown | 17 | 0 | 7 | 24 |
| [docs/FILE\_MANIFEST.md](/docs/FILE_MANIFEST.md) | Markdown | 83 | 0 | 3 | 86 |
| [docs/MOD\_MECHANICS.md](/docs/MOD_MECHANICS.md) | Markdown | 30 | 0 | 12 | 42 |
| [docs/SPEC.md](/docs/SPEC.md) | Markdown | 41 | 0 | 10 | 51 |
| [docs/STATE\_FLOW.md](/docs/STATE_FLOW.md) | Markdown | 18 | 0 | 7 | 25 |
| [src/ComlinkService.cpp](/src/ComlinkService.cpp) | C++ | 33 | 0 | 7 | 40 |
| [src/swgoh-command-bridge.Core/AppDbContext.cs](/src/swgoh-command-bridge.Core/AppDbContext.cs) | C# | 55 | 11 | 13 | 79 |
| [src/swgoh-command-bridge.Core/Database/Entities/CharacterEntity.cs](/src/swgoh-command-bridge.Core/Database/Entities/CharacterEntity.cs) | C# | 16 | 3 | 9 | 28 |
| [src/swgoh-command-bridge.Core/Database/Entities/GameModEntity.cs](/src/swgoh-command-bridge.Core/Database/Entities/GameModEntity.cs) | C# | 16 | 3 | 9 | 28 |
| [src/swgoh-command-bridge.Core/Database/Entities/PlayerEntity.cs](/src/swgoh-command-bridge.Core/Database/Entities/PlayerEntity.cs) | C# | 14 | 3 | 7 | 24 |
| [src/swgoh-command-bridge.Core/Database/Entities/SwgohGgRecommendationEntity.cs](/src/swgoh-command-bridge.Core/Database/Entities/SwgohGgRecommendationEntity.cs) | C# | 13 | 18 | 6 | 37 |
| [src/swgoh-command-bridge.Core/Database/Repositories/IPlayerRepository.cs](/src/swgoh-command-bridge.Core/Database/Repositories/IPlayerRepository.cs) | C# | 12 | 9 | 3 | 24 |
| [src/swgoh-command-bridge.Core/Database/Repositories/PlayerRepository.cs](/src/swgoh-command-bridge.Core/Database/Repositories/PlayerRepository.cs) | C# | 64 | 8 | 14 | 86 |
| [src/swgoh-command-bridge.Core/ISwgohGgScraperService.cs](/src/swgoh-command-bridge.Core/ISwgohGgScraperService.cs) | C# | 13 | 9 | 3 | 25 |
| [src/swgoh-command-bridge.Core/ModAdvisorService.cs](/src/swgoh-command-bridge.Core/ModAdvisorService.cs) | C# | 37 | 3 | 7 | 47 |
| [src/swgoh-command-bridge.Core/ModAssignmentPlan.cs](/src/swgoh-command-bridge.Core/ModAssignmentPlan.cs) | C# | 11 | 4 | 2 | 17 |
| [src/swgoh-command-bridge.Core/ModAssignmentService.cs](/src/swgoh-command-bridge.Core/ModAssignmentService.cs) | C# | 246 | 39 | 37 | 322 |
| [src/swgoh-command-bridge.Core/ModSwapRecommendation.cs](/src/swgoh-command-bridge.Core/ModSwapRecommendation.cs) | C# | 12 | 3 | 1 | 16 |
| [src/swgoh-command-bridge.Core/Models/AppSettings.cs](/src/swgoh-command-bridge.Core/Models/AppSettings.cs) | C# | 10 | 3 | 1 | 14 |
| [src/swgoh-command-bridge.Core/Models/AssignedModDetail.cs](/src/swgoh-command-bridge.Core/Models/AssignedModDetail.cs) | C# | 11 | 3 | 1 | 15 |
| [src/swgoh-command-bridge.Core/Models/Character.cs](/src/swgoh-command-bridge.Core/Models/Character.cs) | C# | 37 | 3 | 5 | 45 |
| [src/swgoh-command-bridge.Core/Models/GameMod.cs](/src/swgoh-command-bridge.Core/Models/GameMod.cs) | C# | 21 | 4 | 4 | 29 |
| [src/swgoh-command-bridge.Core/Models/IModAdvisorService.cs](/src/swgoh-command-bridge.Core/Models/IModAdvisorService.cs) | C# | 15 | 6 | 2 | 23 |
| [src/swgoh-command-bridge.Core/Models/IPlayerService.cs](/src/swgoh-command-bridge.Core/Models/IPlayerService.cs) | C# | 12 | 9 | 3 | 24 |
| [src/swgoh-command-bridge.Core/Models/ModAdvisorService.cs](/src/swgoh-command-bridge.Core/Models/ModAdvisorService.cs) | C# | 71 | 12 | 11 | 94 |
| [src/swgoh-command-bridge.Core/Models/ModEnums.cs](/src/swgoh-command-bridge.Core/Models/ModEnums.cs) | C# | 51 | 1 | 5 | 57 |
| [src/swgoh-command-bridge.Core/Models/ModRecommendation.cs](/src/swgoh-command-bridge.Core/Models/ModRecommendation.cs) | C# | 18 | 21 | 6 | 45 |
| [src/swgoh-command-bridge.Core/Models/ModStat.cs](/src/swgoh-command-bridge.Core/Models/ModStat.cs) | C# | 27 | 3 | 4 | 34 |
| [src/swgoh-command-bridge.Core/Models/ModUpgradeThreshold.cs](/src/swgoh-command-bridge.Core/Models/ModUpgradeThreshold.cs) | C# | 13 | 3 | 1 | 17 |
| [src/swgoh-command-bridge.Core/Models/PlayerProfile.cs](/src/swgoh-command-bridge.Core/Models/PlayerProfile.cs) | C# | 13 | 3 | 2 | 18 |
| [src/swgoh-command-bridge.Core/Models/PlayerService.cs](/src/swgoh-command-bridge.Core/Models/PlayerService.cs) | C# | 218 | 12 | 39 | 269 |
| [src/swgoh-command-bridge.Core/Models/ScrapeProgress.cs](/src/swgoh-command-bridge.Core/Models/ScrapeProgress.cs) | C# | 12 | 3 | 1 | 16 |
| [src/swgoh-command-bridge.Core/Services/ComlinkService.cs](/src/swgoh-command-bridge.Core/Services/ComlinkService.cs) | C# | 63 | 8 | 12 | 83 |
| [src/swgoh-command-bridge.Core/Services/IComlinkService.cs](/src/swgoh-command-bridge.Core/Services/IComlinkService.cs) | C# | 11 | 9 | 3 | 23 |
| [src/swgoh-command-bridge.Core/Services/IModAssignmentService.cs](/src/swgoh-command-bridge.Core/Services/IModAssignmentService.cs) | C# | 15 | 6 | 3 | 24 |
| [src/swgoh-command-bridge.Core/Services/ISettingsService.cs](/src/swgoh-command-bridge.Core/Services/ISettingsService.cs) | C# | 12 | 12 | 4 | 28 |
| [src/swgoh-command-bridge.Core/Services/ModAssignmentService.cs](/src/swgoh-command-bridge.Core/Services/ModAssignmentService.cs) | C# | 107 | 7 | 22 | 136 |
| [src/swgoh-command-bridge.Core/Services/SettingsService.cs](/src/swgoh-command-bridge.Core/Services/SettingsService.cs) | C# | 89 | 14 | 17 | 120 |
| [src/swgoh-command-bridge.Core/SwgohGgScraperService.cs](/src/swgoh-command-bridge.Core/SwgohGgScraperService.cs) | C# | 257 | 14 | 47 | 318 |
| [src/swgoh-command-bridge.Core/swgoh-command-bridge.Core.csproj](/src/swgoh-command-bridge.Core/swgoh-command-bridge.Core.csproj) | XML | 13 | 0 | 4 | 17 |
| [src/swgoh-command-bridge.UI/App.axaml](/src/swgoh-command-bridge.UI/App.axaml) | XML | 11 | 0 | 2 | 13 |
| [src/swgoh-command-bridge.UI/App.axaml.cs](/src/swgoh-command-bridge.UI/App.axaml.cs) | C# | 24 | 0 | 6 | 30 |
| [src/swgoh-command-bridge.UI/Program.cs](/src/swgoh-command-bridge.UI/Program.cs) | C# | 14 | 4 | 4 | 22 |
| [src/swgoh-command-bridge.UI/ViewLocator.cs](/src/swgoh-command-bridge.UI/ViewLocator.cs) | C# | 26 | 0 | 7 | 33 |
| [src/swgoh-command-bridge.UI/ViewModels/CharacterPrioritiesViewModel.cs](/src/swgoh-command-bridge.UI/ViewModels/CharacterPrioritiesViewModel.cs) | C# | 142 | 27 | 17 | 186 |
| [src/swgoh-command-bridge.UI/ViewModels/CharactersViewModel.cs](/src/swgoh-command-bridge.UI/ViewModels/CharactersViewModel.cs) | C# | 103 | 24 | 14 | 141 |
| [src/swgoh-command-bridge.UI/ViewModels/MainWindowViewModel.cs](/src/swgoh-command-bridge.UI/ViewModels/MainWindowViewModel.cs) | C# | 45 | 1 | 11 | 57 |
| [src/swgoh-command-bridge.UI/ViewModels/ModOptimizerViewModel.cs](/src/swgoh-command-bridge.UI/ViewModels/ModOptimizerViewModel.cs) | C# | 212 | 39 | 29 | 280 |
| [src/swgoh-command-bridge.UI/ViewModels/ModThresholdsViewModel.cs](/src/swgoh-command-bridge.UI/ViewModels/ModThresholdsViewModel.cs) | C# | 23 | 9 | 4 | 36 |
| [src/swgoh-command-bridge.UI/ViewModels/ModsViewModel.cs](/src/swgoh-command-bridge.UI/ViewModels/ModsViewModel.cs) | C# | 133 | 27 | 19 | 179 |
| [src/swgoh-command-bridge.UI/ViewModels/ViewModelBase.cs](/src/swgoh-command-bridge.UI/ViewModels/ViewModelBase.cs) | C# | 5 | 0 | 3 | 8 |
| [src/swgoh-command-bridge.UI/Views/CharacterPrioritiesView.axaml](/src/swgoh-command-bridge.UI/Views/CharacterPrioritiesView.axaml) | XML | 16 | 0 | 2 | 18 |
| [src/swgoh-command-bridge.UI/Views/CharacterPrioritiesView.axaml.cs](/src/swgoh-command-bridge.UI/Views/CharacterPrioritiesView.axaml.cs) | C# | 9 | 6 | 2 | 17 |
| [src/swgoh-command-bridge.UI/Views/CharactersView.axaml](/src/swgoh-command-bridge.UI/Views/CharactersView.axaml) | XML | 16 | 0 | 2 | 18 |
| [src/swgoh-command-bridge.UI/Views/CharactersView.axaml.cs](/src/swgoh-command-bridge.UI/Views/CharactersView.axaml.cs) | C# | 9 | 6 | 3 | 18 |
| [src/swgoh-command-bridge.UI/Views/MainWindow.axaml](/src/swgoh-command-bridge.UI/Views/MainWindow.axaml) | XML | 26 | 0 | 5 | 31 |
| [src/swgoh-command-bridge.UI/Views/MainWindow.axaml.cs](/src/swgoh-command-bridge.UI/Views/MainWindow.axaml.cs) | C# | 9 | 0 | 3 | 12 |
| [src/swgoh-command-bridge.UI/Views/ModOptimizerView.axaml](/src/swgoh-command-bridge.UI/Views/ModOptimizerView.axaml) | XML | 45 | 3 | 5 | 53 |
| [src/swgoh-command-bridge.UI/Views/ModOptimizerView.axaml.cs](/src/swgoh-command-bridge.UI/Views/ModOptimizerView.axaml.cs) | C# | 9 | 6 | 2 | 17 |
| [src/swgoh-command-bridge.UI/Views/ModThresholdsView.axaml](/src/swgoh-command-bridge.UI/Views/ModThresholdsView.axaml) | XML | 16 | 0 | 2 | 18 |
| [src/swgoh-command-bridge.UI/Views/ModThresholdsView.axaml.cs](/src/swgoh-command-bridge.UI/Views/ModThresholdsView.axaml.cs) | C# | 9 | 6 | 2 | 17 |
| [src/swgoh-command-bridge.UI/Views/ModsView.axaml](/src/swgoh-command-bridge.UI/Views/ModsView.axaml) | XML | 63 | 5 | 8 | 76 |
| [src/swgoh-command-bridge.UI/Views/ModsView.axaml.cs](/src/swgoh-command-bridge.UI/Views/ModsView.axaml.cs) | C# | 9 | 6 | 3 | 18 |
| [src/swgoh-command-bridge.UI/swgoh-command-bridge.UI.csproj](/src/swgoh-command-bridge.UI/swgoh-command-bridge.UI.csproj) | XML | 23 | 1 | 4 | 28 |
| [tests/swgoh-command-bridge.Tests/UnitTest1.cs](/tests/swgoh-command-bridge.Tests/UnitTest1.cs) | C# | 10 | 0 | 3 | 13 |
| [tests/swgoh-command-bridge.Tests/swgoh-command-bridge.Tests.csproj](/tests/swgoh-command-bridge.Tests/swgoh-command-bridge.Tests.csproj) | XML | 24 | 0 | 6 | 30 |

[Summary](results.md) / Details / [Diff Summary](diff.md) / [Diff Details](diff-details.md)