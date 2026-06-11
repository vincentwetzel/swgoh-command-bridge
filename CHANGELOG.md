# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Added a high-performance, non-allocating `OperationState` record struct in the Core layer to model explicit loading, empty, success, and error states across MVVM viewmodels.
- Added comprehensive unit tests for `PlayerService` verifying live raw json parsing and synchronization pathways.
- Added integration tests for `SettingsService` verifying atomic configuration storage and loaded fallbacks.
- Added a high-performance, non-allocating `ModFilterService` with support for complex secondary combinations, slots, sets, tiers, pips, and equipped states.
- Implemented database validation helper `HasRecommendationAsync` to detect missing community recommendations.
- Added robust, HTML-fixture-based unit tests for `SwgohGgScraperService` verifying standard extraction patterns on simulated page markups.
- Added comprehensive unit tests for `ModAdvisorService` verifying slicing, keeps, and sell scenarios.
- Added high-coverage xUnit unit tests verifying mod quality scoring, potential speed, and slicing recommendations in `ModAdvisorServiceTests`.
- Added `ModMechanicsService` to model secondary roll mechanics and predict maximum potential Speed outcomes.
- Refactored `ModAdvisorService` to dynamically evaluate mod slicing and upgrade paths based on predicted potential stats.
- Implemented comprehensive upgrade/swap/sell recommendation logic in `ModAdvisorService` performing deep comparison checks against equipped mods on high-priority roster characters.
- Implemented persistence for user-defined mod upgrade thresholds inside AppSettings.
- Added a complete accounts sync workflow in `PlayerService` that retrieves player profiles, maps them to database entities, and caches them inside the local SQLite database via `PlayerRepository`.
- Added cross-platform configuration and dynamic settings persistence with atomic write-safety safeguards (`SettingsService`).
- Fully implemented `PlayerService` parsing to recursively map real `swgoh-comlink` player profile, roster unit, and equipped stat mod payloads.
- Added progress reporting, cooperative cancellation, and failure tracking to the incremental `swgoh.gg` scraper using `IProgress<ScrapeProgress>`.
- Implemented explicit "Roster Coverage" metrics and automatic fallback mod assignment in `ModAssignmentService`.
- Integrated detailed assignment explanations via `AssignedModDetail` explaining why each mod was selected (Set Match, Primary Match, Speed levels).
- Fully implemented flexible recommendation engine logic that weights primary stats and mod sets dynamically based on usage percentages.
- Added support for scraping and storing custom usage percentage models for character recommendations.
- Added stale-data protection checks to `SwgohGgScraperService` to skip scraping characters updated in the last 7 days.
- Integrated swgoh.gg community recommendations into `ModAssignmentService` with high-performance slot primary and set-matching logic.
- Fully implemented `ModAssignmentService` with high-performance assignment logic that optimizes mods based on character priority.
- Added `ModAssignmentPlan` and `ModSwapRecommendation` domain records.
- Support for generating explicit swap recommendations and ensuring zero duplicate mod reuse across the roster during assignments.
- Standardized structured logging and defensive guard clauses in `ModAssignmentService` to comply with the project standards.
- Initial project structure with `Core`, `UI`, and `Tests` projects.
- Basic Avalonia MVVM setup in the `UI` project.
- Core data models for `GameMod`, `Character`, `ModStat`, and enums.
- Initial project documentation (`README`, `ARCHITECTURE`, `SPEC`, `TODO`, `CODING_STANDARDS`).
- Character, mod inventory, priority, threshold, and optimizer view/view-model scaffolds.
- Early mod advisor, player service, and mod assignment service scaffolds in the compiled Core project.
- Root-level EF Core entity, repository, Comlink, `swgoh.gg` scraper, and mod assignment integration drafts.
- Local SQLite package reference for the Core project.
- Compiled Core `SwgohGgScraperService` and interface for scraping best-mod pages, retrying transient failures, and persisting set/primary recommendation JSON.
- SQLite-backed UI viewmodel behavior for character lists, character priorities, mod filtering/sorting, selected-mod recommendations, and optimizer community recommendation display.

### Changed
- **Project Scope:** The primary goal has been refocused to be a **read-only** analysis and recommendation tool. Features related to direct account modification (e.g., equipping or upgrading mods) have been moved to stretch goals.
- Documentation refreshed to reflect the current mixed state of compiled project scaffolding, root-level persistence drafts, and the Core `swgoh.gg` scraper.
