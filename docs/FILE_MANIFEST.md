# File Manifest

This document lists the current tracked source and documentation files in the project, organized by directory.

The active implementation is under `src/`; only the project files under `src/` are compiled. Older drafts are retained under `legacy/` for reference. Generated folders such as `bin/`, `obj/`, `.tmp/`, `.tmp-build/`, and `.VSCodeCounter/` are intentionally omitted.

```text
.
|-- .gitignore
|-- CHANGELOG.md
|-- CODING_STANDARDS.md
|-- README.md
|-- TODO.md
|-- swgoh-command-bridge.sln
|-- legacy
|   |-- CharacterEntity.cs
|   |-- ComlinkService.cs
|   |-- GameModEntity.cs
|   |-- IComlinkService.cs
|   |-- IModAssignmentService.cs
|   |-- IPlayerRepository.cs
|   |-- ISwgohGgScraperService.cs
|   |-- ModAssignmentService.cs
|   |-- PlayerEntity.cs
|   |-- PlayerRepository.cs
|   |-- SwgohGgRecommendationEntity.cs
|   |-- SwgohGgScraperService.cs
|   `-- build-probe.tmp
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
|   |       |-- AllyCodeValidator.cs
|   |       |-- AppDataPaths.cs
|   |       |-- AppSettings.cs
|   |       |-- AssignedModDetail.cs
|   |       |-- Character.cs
|   |       |-- GameMod.cs
|   |       |-- IModAdvisorService.cs
|   |       |-- IPlayerService.cs
|   |       |-- ModAdvisorService.cs
|   |       |-- ModAssignmentAlternative.cs
|   |       |-- ModEnums.cs
|   |       |-- ModLoadoutResult.cs
|   |       |-- ModLoadoutProjection.cs
|   |       |-- ModRecommendation.cs
|   |       |-- ModStat.cs
|   |       |-- ModThresholdTransferDocument.cs
|   |       |-- ModUpgradeThreshold.cs
|   |       |-- OperationState.cs
|   |       |-- PlayerProfile.cs
|   |       |-- PlayerService.cs
|   |       |-- PlayerSyncDiagnostics.cs
|   |       |-- PlayerSyncProgress.cs
|   |       |-- RecommendationSnapshot.cs
|   |       |-- RosterLoadoutPlan.cs
|   |       |-- ScrapeCharacterResult.cs
|   |       |-- ScrapeProgress.cs
|   |       |-- ScrapeRetryPolicy.cs
|   |       |-- ThemePreference.cs
|   |       `-- SettingsTransferDocument.cs
|   |   `-- Services
|   |       |-- CharacterMetadataParser.cs
|   |       |-- ComlinkErrorFormatter.cs
|   |       |-- ComlinkRuntimeManager.cs
|   |       |-- ComlinkService.cs
|   |       |-- DiagnosticEventLog.cs
|   |       |-- DiagnosticLogger.cs
|   |       |-- IComlinkService.cs
|   |       |-- IComlinkRuntimeManager.cs
|   |       |-- IModAssignmentService.cs
|   |       |-- ISettingsService.cs
|   |       |-- ModAssignmentService.cs
|   |       |-- ModFilterService.cs
|   |       |-- ModMechanicsService.cs
|   |       |-- ModThresholdTransferService.cs
|   |       |-- PersistedModelMapper.cs
|   |       |-- PlayerProfileParser.cs
|   |       |-- SecondaryStatFilterService.cs
|   |       |-- SettingsService.cs
|   |       |-- SettingsMigrationService.cs
|   |       |-- SettingsTransferService.cs
|   |       `-- SwgohGgRecommendationParser.cs
|   `-- swgoh-command-bridge.UI
|       |-- ApplicationComposition.cs
|       |-- App.axaml
|       |-- App.axaml.cs
|       |-- Program.cs
|       |-- ViewLocator.cs
|       |-- app.manifest
|       |-- swgoh-command-bridge.UI.csproj
|       |-- ThemeManager.cs
|       |-- ViewModels
|       |   |-- CharacterPrioritiesViewModel.cs
|       |   |-- CharactersViewModel.cs
|       |   |-- DiagnosticsViewModel.cs
|       |   |-- MainWindowViewModel.cs
|       |   |-- ModOptimizerViewModel.cs
|       |   |-- ModThresholdsViewModel.cs
|       |   |-- SettingsViewModel.cs
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
        |-- ModAdvisorServiceTests.cs
        |-- ModAdvisorDecisionMatrixTests.cs
        |-- ModAssignmentServiceTests.cs
        |-- ModThresholdsViewModelTests.cs
        |-- ModsViewModelTests.cs
        |-- ComlinkErrorFormatterTests.cs
        |-- ComlinkServiceTests.cs
        |-- DiagnosticEventLogTests.cs
        |-- DiagnosticLoggerTests.cs
        |-- DiagnosticsViewModelTests.cs
        |-- ModFilterServiceTests.cs
        |-- OperationStateTests.cs
        |-- PlayerRepositoryTests.cs
        |-- PlayerServiceTests.cs
        |-- PersistedModelMapperTests.cs
        |-- SecondaryStatFilterServiceTests.cs
        |-- SettingsServiceTests.cs
        |-- SettingsTransferServiceTests.cs
        |-- SettingsViewModelTests.cs
        |-- StateViewModelBaseTests.cs
        |-- CharacterViewModelTests.cs
        |-- CharacterMetadataParserTests.cs
        |-- ModOptimizerViewModelTests.cs
        |-- MainWindowViewModelTests.cs
        |-- ApplicationCompositionTests.cs
        |-- ViewModelErrorStateTests.cs
        |-- ModThresholdTransferServiceTests.cs
        |-- RecommendationSnapshotTests.cs
        |-- SwgohGgRecommendationParserTests.cs
        |-- SwgohGgScraperServiceTests.cs
        |-- ScrapeRetryPolicyTests.cs
        |-- SyncHistoryRepositoryTests.cs
        |-- Fixtures
        |   |-- ComlinkPayloadFixtures.cs
        |   `-- RecommendationPageFixtures.cs
        `-- swgoh-command-bridge.Tests.csproj
```
