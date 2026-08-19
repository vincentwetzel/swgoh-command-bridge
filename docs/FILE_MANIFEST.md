# File Manifest

This document lists the current source and documentation files in the repository, organized by directory. The active implementation is under `src/`; only the projects under `src/` are compiled. Generated folders such as `bin/`, `obj/`, `.build-*`, `.tmp*`, and `.VSCodeCounter/` are intentionally omitted.

```text
.
|-- .aiexclude
|-- .gitignore
|-- CHANGELOG.md
|-- CODING_STANDARDS.md
|-- README.md
|-- TODO.md
|-- swgoh-command-bridge.sln
|-- docs
|   |-- AGENTS.md
|   |-- ARCHITECTURE.md
|   |-- COMLINK_SETUP.md
|   |-- DIAGNOSTICS.md
|   |-- FILE_MANIFEST.md
|   |-- MOD_MECHANICS.md
|   |-- RELEASE_GUIDE.md
|   |-- ROADMAP_HISTORY.md
|   |-- SMOKE_TEST_CHECKLIST.md
|   |-- SPEC.md
|   `-- STATE_FLOW.md
|-- src
|   |-- ComlinkService.cpp
|   |-- ComlinkService.hpp
|   |-- swgoh-command-bridge.Core
|   |   |-- AppDbContext.cs
|   |   |-- ISwgohGgScraperService.cs
|   |   |-- ModAdvisorService.cs
|   |   |-- ModAssignmentPlan.cs
|   |   |-- ModAssignmentService.cs
|   |   |-- ModSwapRecommendation.cs
|   |   |-- SwgohGgScraperService.cs
|   |   |-- swgoh-command-bridge.Core.csproj
|   |   |-- Assets
|   |   |   `-- CharacterCatalog
|   |   |       |-- swgoh-characters.json
|   |   |       `-- swgoh-ships.json
|   |   |-- Database
|   |   |   |-- CacheSchemaMigrator.cs
|   |   |   |-- Entities
|   |   |   |   |-- CharacterEntity.cs
|   |   |   |   |-- GameModEntity.cs
|   |   |   |   |-- PlayerEntity.cs
|   |   |   |   |-- SyncHistoryEntity.cs
|   |   |   |   `-- SwgohGgRecommendationEntity.cs
|   |   |   `-- Repositories
|   |   |       |-- IPlayerRepository.cs
|   |   |       |-- ISyncHistoryRepository.cs
|   |   |       |-- PlayerRepository.cs
|   |   |       `-- SyncHistoryRepository.cs
|   |   |-- Models
|   |   |   |-- AllyCodeValidator.cs
|   |   |   |-- AppDataPaths.cs
|   |   |   |-- AppSettings.cs
|   |   |   |-- AssignedModDetail.cs
|   |   |   |-- Character.cs
|   |   |   |-- GameMod.cs
|   |   |   |-- IModAdvisorService.cs
|   |   |   |-- IPlayerService.cs
|   |   |   |-- ModAdvisorService.cs
|   |   |   |-- ModAssignmentAlternative.cs
|   |   |   |-- ModEnums.cs
|   |   |   |-- ModLoadoutProjection.cs
|   |   |   |-- ModLoadoutResult.cs
|   |   |   |-- ModRecommendation.cs
|   |   |   |-- ModStat.cs
|   |   |   |-- ModThresholdTransferDocument.cs
|   |   |   |-- ModUpgradeThreshold.cs
|   |   |   |-- OperationState.cs
|   |   |   |-- PlayerProfile.cs
|   |   |   |-- PlayerService.cs
|   |   |   |-- PlayerSyncDiagnostics.cs
|   |   |   |-- PlayerSyncProgress.cs
|   |   |   |-- RecommendationSnapshot.cs
|   |   |   |-- RosterLoadoutPlan.cs
|   |   |   |-- ScrapeCharacterResult.cs
|   |   |   |-- ScrapeProgress.cs
|   |   |   |-- ScrapeRetryPolicy.cs
|   |   |   |-- SettingsTransferDocument.cs
|   |   |   `-- ThemePreference.cs
|   |   `-- Services
|   |       |-- BundledCharacterCatalogService.cs
|   |       |-- CharacterCatalogParser.cs
|   |       |-- CharacterMetadataParser.cs
|   |       |-- CharacterNameFormatter.cs
|   |       |-- ComlinkCatalogRefreshService.cs
|   |       |-- ComlinkErrorFormatter.cs
|   |       |-- ComlinkRuntimeManager.cs
|   |       |-- ComlinkService.cs
|   |       |-- DiagnosticEventLog.cs
|   |       |-- DiagnosticLogger.cs
|   |       |-- IComlinkRuntimeManager.cs
|   |       |-- IComlinkService.cs
|   |       |-- IModAssignmentService.cs
|   |       |-- ISettingsService.cs
|   |       |-- ModAssignmentService.cs
|   |       |-- ModFilterService.cs
|   |       |-- ModMechanicsService.cs
|   |       |-- ModThresholdTransferService.cs
|   |       |-- PersistedModelMapper.cs
|   |       |-- PlayerProfileParser.cs
|   |       |-- SecondaryStatFilterService.cs
|   |       |-- SettingsMigrationService.cs
|   |       |-- SettingsService.cs
|   |       |-- SettingsTransferService.cs
|   |       `-- SwgohGgRecommendationParser.cs
|   |-- swgoh-command-bridge.UI
|       |-- App.axaml
|       |-- App.axaml.cs
|       |-- app.manifest
|       |-- ApplicationComposition.cs
|       |-- Program.cs
|       |-- swgoh-command-bridge.UI.csproj
|       |-- ThemeManager.cs
|       |-- ViewLocator.cs
|       |-- Converters
|       |   `-- CharacterPortraitConverter.cs
|       |-- ViewModels
|       |   |-- CharacterPrioritiesViewModel.cs
|       |   |-- CharactersViewModel.cs
|       |   |-- DiagnosticsViewModel.cs
|       |   |-- MainWindowViewModel.cs
|       |   |-- ModOptimizerViewModel.cs
|       |   |-- ModThresholdsViewModel.cs
|       |   |-- ModsViewModel.cs
|       |   |-- StateViewModelBase.cs
|       |   `-- ViewModelBase.cs
|       `-- Views
|           |-- CharacterPrioritiesView.axaml
|           |-- CharacterPrioritiesView.axaml.cs
|           |-- CharactersView.axaml
|           |-- CharactersView.axaml.cs
|           |-- DiagnosticsView.axaml
|           |-- DiagnosticsView.axaml.cs
|           |-- HomeView.axaml
|           |-- HomeView.axaml.cs
|           |-- MainWindow.axaml
|           |-- MainWindow.axaml.cs
|           |-- ModOptimizerView.axaml
|           |-- ModOptimizerView.axaml.cs
|           |-- ModThresholdsView.axaml
|           |-- ModThresholdsView.axaml.cs
|           |-- ModsView.axaml
|           |-- ModsView.axaml.cs
|           |-- SettingsView.axaml
|           `-- SettingsView.axaml.cs
`-- tests
    `-- swgoh-command-bridge.Tests
        |-- AllyCodeValidatorTests.cs
        |-- AppDataPathsTests.cs
        |-- ApplicationCompositionTests.cs
        |-- CharacterCatalogParserTests.cs
        |-- CharacterMetadataParserTests.cs
        |-- CharacterViewModelTests.cs
        |-- ComlinkErrorFormatterTests.cs
        |-- ComlinkServiceTests.cs
        |-- DiagnosticEventLogTests.cs
        |-- DiagnosticLoggerTests.cs
        |-- DiagnosticsViewModelTests.cs
        |-- MainWindowViewModelTests.cs
        |-- ModAdvisorDecisionMatrixTests.cs
        |-- ModAdvisorServiceTests.cs
        |-- ModAssignmentServiceTests.cs
        |-- ModFilterServiceTests.cs
        |-- ModOptimizerViewModelTests.cs
        |-- ModThresholdTransferServiceTests.cs
        |-- ModThresholdsViewModelTests.cs
        |-- ModsViewModelTests.cs
        |-- OperationStateTests.cs
        |-- PersistedModelMapperTests.cs
        |-- PlayerRepositoryTests.cs
        |-- PlayerServiceTests.cs
        |-- RecommendationSnapshotTests.cs
        |-- ScrapeRetryPolicyTests.cs
        |-- SecondaryStatFilterServiceTests.cs
        |-- SettingsServiceTests.cs
        |-- SettingsTransferServiceTests.cs
        |-- SettingsViewModelTests.cs
        |-- StateViewModelBaseTests.cs
        |-- SwgohGgRecommendationParserTests.cs
        |-- SwgohGgScraperServiceTests.cs
        |-- SyncHistoryRepositoryTests.cs
        |-- ViewModelErrorStateTests.cs
        |-- Fixtures
        |   |-- ComlinkPayloadFixtures.cs
        |   `-- RecommendationPageFixtures.cs
        `-- swgoh-command-bridge.Tests.csproj
```

## Asset and generated-file boundaries

- Core embeds the verified fallback character and ship catalogs from `src/swgoh-command-bridge.Core/Assets/CharacterCatalog/`.
- The UI packages Avalonia resources and links portrait PNGs from the sibling `swgoh-command-bridge-assets` workspace when available.
- Runtime catalog snapshots, SQLite cache, settings, backups, diagnostics, and managed Windows Comlink binaries live below the platform-local `SWGOHCommandBridge` application-data directory; they are not repository files.
- Build, test, publish, and temporary folders are not source inputs and should not be added to this manifest.
