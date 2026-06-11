# TODO Roadmap

This document outlines the development plan for the SWGOH Command Bridge.

## Milestone 1: Core Scaffolding
*Objective: Establish the basic structure of the application, data models, and services.*

- [x] `ComlinkService` for local `swgoh-comlink` API requests.
- [x] Core Models (`Character`, `GameMod`, etc.).
- [x] `PlayerService` to fetch and manage player profile, characters, and mods.
- [ ] Move root-level `DbContext`, entity, and repository drafts into the compiled Core project.
- [x] Basic UI scaffolding with `ModsView` and `CharactersView`.
- [x] Basic view navigation between different sections of the app.
- [ ] Ensure the solution builds and runs on all target platforms.

## Milestone 2: Mod Analysis & User-Defined Rules
*Objective: Implement features for analyzing mods and allowing users to define their own rules and priorities.*

- [ ] **Character Priorities:**
    - [x] Add `Priority` property to the `Character` database entity model.
    - [x] Create a `CharacterPrioritiesView` to allow users to set a priority score for each character.
- [ ] **Mod Upgrade Advisor:**
    - [x] Create `ModUpgradeThreshold` model for user-defined upgrade rules.
    - [x] Implement a `ModThresholdsView` for users to create and manage these thresholds.
    - [x] Implement the `ModAdvisorService` with the core logic for upgrade/swap/sell recommendations.
- [ ] **Detailed Mod Mechanics:**
    - [x] Ensure all mod logic correctly implements the processes described in `docs/MOD_MECHANICS.md` (leveling vs. slicing).
- [ ] **Advanced UI:**
    - [x] Implement advanced filtering and sorting for the mods grid.
    - [x] Create a "Selected Mod" panel to display detailed stats and the recommendation from the `ModAdvisorService`.

## Milestone 3: `swgoh.gg` Integration & Recommendation Engine
*Objective: Scrape data from `swgoh.gg` to power an intelligent and flexible recommendation engine.*

- [ ] **`swgoh.gg` Scraper:**
    - [ ] Move the root-level `SwgohGgScraperService` draft into the compiled Core project.
    - [ ] Replace placeholder parsing with real "best mods" extraction.
    - [ ] Wire SQLite tables for storing scraped mod set and primary stat recommendations.
    - [x] Draft a slow and incremental scraping strategy to avoid overloading `swgoh.gg`.
- [ ] **Recommendation Logic:**
    - [ ] Wire `ModAssignmentService` and `ModAdvisorService` to persisted `swgoh.gg` data.
    - [ ] Implement the "flexible recommendations" logic, considering competitive alternatives based on usage percentages.
    - [x] Draft "roster coverage" logic, prioritizing getting some mod on every character.
- [ ] **UI for Recommendations:**
    - [x] Create a `ModOptimizerView` that allows a user to select a character and see the optimal mod loadout based on their inventory and the `swgoh.gg` data.

## Backlog / Future Scope
- [ ] **Centralized Server:** Design and implement a central server to store `swgoh.gg` data, so individual users don't need to scrape it.
- [ ] **Automated Actions (Write-Access):**
    - [ ] Secure login to the game account.
    - [ ] Implement logic to automatically equip, swap, and upgrade mods.
- [ ] Save/load multiple mod loadouts per character.
- [ ] Grand Arena multi-squad mod management.
- [ ] Data visualizations and dashboards.
- [ ] Theme customization.
