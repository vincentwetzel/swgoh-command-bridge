# File Manifest

This document lists the current files in the project, organized by directory.

```text
.
|-- .gitignore
|-- CHANGELOG.md
|-- CharacterEntity.cs
|-- CODING_STANDARDS.md
|-- ComlinkService.cs
|-- ComlinkService.hpp
|-- GameModEntity.cs
|-- IComlinkService.cs
|-- IModAssignmentService.cs
|-- IPlayerRepository.cs
|-- ISwgohGgScraperService.cs
|-- ModAssignmentService.cs
|-- PlayerEntity.cs
|-- PlayerRepository.cs
|-- README.md
|-- SwgohGgRecommendationEntity.cs
|-- SwgohGgScraperService.cs
|-- TODO.md
|-- swgoh-command-bridge.sln
|-- docs
|   |-- AGENTS.md
|   |-- ARCHITECTURE.md
|   |-- COMLINK_SETUP.md
|   |-- FILE_MANIFEST.md
|   |-- MOD_MECHANICS.md
|   |-- SPEC.md
|   `-- STATE_FLOW.md
|-- src
|   |-- ComlinkService.cpp
|   |-- swgoh-command-bridge.Core
|   |   |-- AppDbContext.cs
|   |   |-- ISwgohGgScraperService.cs
|   |   |-- ModAdvisorService.cs
|   |   |-- ModAssignmentPlan.cs
|   |   |-- ModAssignmentService.cs
|   |   |-- ModSwapRecommendation.cs
|   |   |-- SwgohGgScraperService.cs
|   |   |-- swgoh-command-bridge.Core.csproj
|   |   |-- Database
|   |   |   |-- Entities
|   |   |   |   |-- CharacterEntity.cs
|   |   |   |   |-- GameModEntity.cs
|   |   |   |   |-- PlayerEntity.cs
|   |   |   |   `-- SwgohGgRecommendationEntity.cs
|   |   |   `-- Repositories
|   |   |       |-- IPlayerRepository.cs
|   |   |       `-- PlayerRepository.cs
|   |   |-- Models
|   |       |-- AppSettings.cs
|   |       |-- AssignedModDetail.cs
|   |       |-- Character.cs
|   |       |-- GameMod.cs
|   |       |-- IModAdvisorService.cs
|   |       |-- IPlayerService.cs
|   |       |-- ModAdvisorService.cs
|   |       |-- ModEnums.cs
|   |       |-- ModRecommendation.cs
|   |       |-- ModStat.cs
|   |       |-- ModUpgradeThreshold.cs
|   |       |-- OperationState.cs
|   |       |-- PlayerProfile.cs
|   |       |-- PlayerService.cs
|   |       `-- ScrapeProgress.cs
|   |   `-- Services
|   |       |-- ComlinkService.cs
|   |       |-- IComlinkService.cs
|   |       |-- IModAssignmentService.cs
|   |       |-- ISettingsService.cs
|   |       |-- ModAssignmentService.cs
|   |       |-- ModFilterService.cs
|   |       |-- ModMechanicsService.cs
|   |       `-- SettingsService.cs
|   `-- swgoh-command-bridge.UI
|       |-- App.axaml
|       |-- App.axaml.cs
|       |-- Program.cs
|       |-- ViewLocator.cs
|       |-- app.manifest
|       |-- swgoh-command-bridge.UI.csproj
|       |-- ViewModels
|       |   |-- CharacterPrioritiesViewModel.cs
|       |   |-- CharactersViewModel.cs
|       |   |-- MainWindowViewModel.cs
|       |   |-- ModOptimizerViewModel.cs
|       |   |-- ModThresholdsViewModel.cs
|       |   |-- ModsViewModel.cs
|       |   `-- ViewModelBase.cs
|       `-- Views
|           |-- CharacterPrioritiesView.axaml
|           |-- CharacterPrioritiesView.axaml.cs
|           |-- CharactersView.axaml
|           |-- CharactersView.axaml.cs
|           |-- MainWindow.axaml
|           |-- MainWindow.axaml.cs
|           |-- ModOptimizerView.axaml
|           |-- ModOptimizerView.axaml.cs
|           |-- ModThresholdsView.axaml
|           |-- ModThresholdsView.axaml.cs
|           |-- ModsView.axaml
|           `-- ModsView.axaml.cs
`-- tests
    `-- swgoh-command-bridge.Tests
        |-- ModAdvisorServiceTests.cs
        |-- ModFilterServiceTests.cs
        |-- OperationStateTests.cs
        |-- PlayerServiceTests.cs
        |-- SettingsServiceTests.cs
        |-- UnitTest1.cs
        `-- swgoh-command-bridge.Tests.csproj
```
