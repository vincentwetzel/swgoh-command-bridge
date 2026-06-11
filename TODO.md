# TODO Roadmap

This document outlines the development plan for the SWGOH Command Bridge.

## Milestone 1: Core Scaffolding
*Objective: Establish the basic structure of the application, data models, and services.*

- [x] `ComlinkService` for local `swgoh-comlink` API requests.
- [x] Core Models (`Character`, `GameMod`, etc.).
- [x] `PlayerService` to fetch and manage player profile, characters, and mods.
- [x] Move root-level `DbContext`, entity, and repository drafts into the compiled Core project.
- [x] Wire `PlayerService` to fully map `swgoh-comlink` roster/mod payloads into Character, GameMod, and persisted EF entities.
- [x] Add an account sync workflow that fetches by ally code, saves through `PlayerRepository`, and refreshes the UI from SQLite.
- [x] Add configuration for the local `swgoh-comlink` base URL instead of assuming `http://localhost:3000` everywhere.
- [x] Basic UI scaffolding with `ModsView` and `CharactersView`.
- [x] Basic view navigation between different sections of the app.
- [ ] Ensure the solution builds and runs on all target platforms.
- [x] Replace preview/mock fallback data in UI viewmodels with explicit empty, loading, and error states.
- [x] Add focused xUnit coverage for core services and database mappings; replace the scaffold `UnitTest1`.

## Milestone 2: Mod Analysis & User-Defined Rules
*Objective: Implement features for analyzing mods and allowing users to define their own rules and priorities.*

- [x] **Character Priorities:**
    - [x] Add `Priority` property to the `Character` database entity model.
    - [x] Create a `CharacterPrioritiesView` to allow users to set a priority score for each character.
- [x] **Mod Upgrade Advisor:**
    - [x] Create `ModUpgradeThreshold` model for user-defined upgrade rules.
    - [x] Implement a `ModThresholdsView` for users to create and manage these thresholds.
    - [x] Persist user-defined thresholds and load them into `ModAdvisorService` instead of relying on in-memory/default rules.
    - [x] Implement full upgrade/swap/sell recommendations, including comparison against equipped mods on high-priority characters.
- [x] **Detailed Mod Mechanics:**
    - [x] Model secondary stat rolls and potential future rolls at levels 3/6/9/12 and during slicing.
    - [x] Ensure advisor decisions account for current stats plus potential stats as described in `docs/MOD_MECHANICS.md`.
- [x] **Advanced UI:**
    - [x] Implement advanced filtering and sorting for the mods grid.
    - [x] Create a "Selected Mod" panel to display detailed stats and the recommendation from the `ModAdvisorService`.
    - [x] Add quick search/filtering by secondary stat combinations, equipped status, slot, set, primary stat, level, pips, and tier.

## Milestone 3: `swgoh.gg` Integration & Recommendation Engine
*Objective: Scrape data from `swgoh.gg` to power an intelligent and flexible recommendation engine.*

- [x] **`swgoh.gg` Scraper:**
    - [x] Move the root-level `SwgohGgScraperService` draft into the compiled Core project.
    - [x] Replace placeholder parsing with real "best mods" extraction.
    - [x] Wire SQLite tables for storing scraped mod set and primary stat recommendations.
    - [x] Draft a slow and incremental scraping strategy to avoid overloading `swgoh.gg`.
    - [x] Store usage percentages per mod set and per slot primary instead of a single default popularity value.
    - [x] Add stale-data checks so already-scraped character recommendations are updated infrequently.
    - [x] Add a user-triggered incremental scrape command with progress, cancellation, and failure reporting.
    - [x] Harden parsing with fixture-based tests for current `swgoh.gg` best-mods page HTML.
- [x] **Recommendation Logic:**
    - [x] Wire `ModAssignmentService` and `ModAdvisorService` to persisted `swgoh.gg` data.
    - [x] Implement the "flexible recommendations" logic, considering competitive alternatives based on usage percentages.
    - [x] Draft "roster coverage" logic, prioritizing getting some mod on every character.
    - [x] Score primary stats by matching the mod's actual primary stat to the recommended primary for its slot.
    - [x] Enforce valid complete mod loadouts, including six slots, no duplicate mod reuse across planned assignments, and set-bonus requirements.
    - [x] Generate explicit mod swap suggestions that identify source character, destination character, and expected benefit.
- [x] **UI for Recommendations:**
    - [x] Create a `ModOptimizerView` that allows a user to select a character and see the optimal mod loadout based on their inventory and the `swgoh.gg` data.
    - [x] Show why each recommended mod was selected, including set match, primary match, secondary stat value, and swap impact.
    - [x] Add states for missing community data and offer to scrape/update recommendations from the optimizer view.

## Backlog / Future Scope
- [ ] **Centralized Server:** Design and implement a central server to store `swgoh.gg` data, so individual users don't need to scrape it.
- [ ] **Automated Actions (Write-Access):**
    - [ ] Secure login to the game account.
    - [ ] Implement logic to automatically equip, swap, and upgrade mods.
- [ ] Save/load multiple mod loadouts per character.
- [ ] Grand Arena multi-squad mod management.
- [ ] Data visualizations and dashboards.
- [ ] Theme customization.
