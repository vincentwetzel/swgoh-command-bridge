# Architecture Overview

This project follows a clean, decoupled architecture designed for a .NET 8 desktop application using the Model-View-ViewModel (MVVM) pattern. Avalonia and the Core library target multiple desktop platforms; automatic Comlink installation is currently implemented for Windows x64 only.

The active implementation is compiled from `src/`. The desktop composition root owns one service graph for the process lifetime; there is no long-running in-app worker. Startup may launch one stale-active-account refresh plus best-effort character-catalog and preferred-mod dataset refreshes after cached data is loaded, without blocking the initial cached-data view. A maintainer runs the local preferred-mod publisher when a new aggregate dataset is wanted.

## Projects

The solution is divided into three main projects. A few older root-level `.cs` drafts still exist for reference, but active application code is compiled from the `src/` project directories.

### 1. `swgoh-command-bridge.Core`
*   **Purpose:** This is the business logic and data layer of the application. It is a standard .NET library with no dependencies on any UI framework.
*   **Contents:**
*   **Models:** Plain C# record types (`GameMod`, `Character`, `PlayerProfile`, etc.) plus small state/configuration records such as `OperationState<T>`, `AppSettings`, `PriorityTier`, `ModPrimaryRules`, shared application-data paths, ally-code validation, player-sync diagnostics, and scraper progress models. Cached player entities retain the last successful sync timestamp and priority-board placement.
*   **Services:** Classes responsible for fetching, caching, filtering, analyzing, and assigning data. This includes Comlink access, privacy-safe `ComlinkErrorFormatter` failure classification, tolerant `PlayerProfileParser` mapping with catalog/metadata name enrichment and primary normalization, `CharacterCatalogParser`, `BundledRosterUnitClassifier`, embedded/persisted catalog services, the embedded/cached preferred-mod dataset service, player sync with bounded durable outcome history, settings persistence and secret-safe transfer, threshold transfer, mod filtering/mechanics, mod upgrade advice, priority-first roster assignment planning, and `swgoh.gg` scraping. `PreferredModsAggregator` is used by the offline publisher to convert high-ranking GAC profiles into aggregate set and primary distributions; raw profiles are not published. `PlayerRepository` owns transactional replacement and account-cache deletion so UI account management does not implement persistence rules itself.
    *   **Data:** EF Core and SQLite are compiled in the Core project. `AppDbContext`, database entities, and repositories live under `src/swgoh-command-bridge.Core/Database`. `SwgohGgRecommendationEntity` stores JSON payloads for recommended sets and slot primary stats under a composite character/ally-code key, while player, character, and mod entities cache synced account data.

### 2. `swgoh-command-bridge.UI`
*   **Purpose:** The presentation layer, built with **Avalonia UI**. This project is responsible for everything the user sees and interacts with.
*   **Pattern:** It strictly follows the **MVVM** pattern to ensure a clean separation between the UI (the "View") and the application logic (the "ViewModel").
*   **Contents:**
    *   **Views:** `.axaml` files that define the UI layout and controls. The code-behind (`.axaml.cs`) is kept minimal.
*   **ViewModels:** Classes that expose data from the `Core` models to the `Views` and handle user commands. `StateViewModelBase<T>` centralizes loading/empty/success/error projections and transition notifications, while feature viewmodels retain specialized flags for filtering, scraping, selections, and status text. The shell groups primary tasks into three workspaces: **Roster** (character inspection plus separate character/ship priority boards), **Mods** (inventory inspection plus upgrade-rule configuration), and **Optimize** (community recommendation context, selected-character loadouts, and priority-roster planning). Dashboard, Settings, and Diagnostics are support surfaces for account management and local application operations. The main window lists cached accounts, reports active character/mod counts, and switches the active ally-code scope offline; `CharacterPrioritiesViewModel` persists tier plus order; character viewmodels query SQLite-backed data; the mods viewmodel filters, deterministically sorts, pages, labels mod inventory, renders readable stat details and advisor efficiency estimates, and guards asynchronous advisor results against stale selections. `CharacterPortraitView` centralizes portrait, gear/relic frame, and initials fallback rendering across roster, priority, and optimizer screens. Settings explicitly controls whether new local `swgoh.gg` scraping requests are allowed; cached recommendations remain readable when that policy is disabled. Feature screens use explicit empty/loading/success/error state instead of preview fallback data.
*   **Composition:** `src/swgoh-command-bridge.UI/ApplicationComposition.cs` is the desktop composition root. It creates the shared database, settings, Comlink client/service, repositories, player service, advisor, assignment service, scraper, and preferred-mod dataset client/service, while `App` disposes the owned database and HTTP resources when the main window closes. `AppDataPaths` provides one case-stable application-data directory for cache, settings, diagnostics, backups, and preferred-mod data; composition accepts injected settings and an optional player service for isolated shell and sync-lifecycle tests.
*   **Diagnostics:** `DiagnosticsViewModel` reads only local metadata, aggregate cache counts, and bounded sync outcome history, includes a bounded in-memory `DiagnosticEventLog` plus `DiagnosticLogger<T>` capture for Core service activity, and exports a privacy-redacted report without account payloads or credentials.
*   **Character catalog:** `BundledCharacterCatalogService` provides the embedded fallback catalog. `ComlinkCatalogRefreshService` validates and atomically persists newer Comlink character/ship catalogs under application data. Catalog parsing resolves localized names, portrait asset names, alignment, duplicate IDs, and audit counts; the UI uses bundled portraits, alignment-aware gear/relic highlights, and an initials fallback.
*   **Recommendation contract:** `SwgohGgRecommendationParser` converts page markup into a fixture-testable `SwgohGgRecommendationParseResult`; `RecommendationSnapshot` is then the shared boundary for persisted community data. The database retains the source, payload schema version, source URL, scrape time, set percentages, and per-slot primary recommendations for assignment and UI consumers. `ModLoadoutResult` carries completeness, set-rule validity, status, per-mod explanations, popularity-weighted lower-ranked alternatives, assignment-score deltas, equipped-mod swap candidates, and conservative persisted mod-stat projections back to the optimizer, while `RosterLoadoutResult` supports both priority-first and bounded joint coverage, plus consolidated swap candidates annotated with inventory availability, reservation, missing-slot, and invalid-set conflict context.
    *   **ViewLocator:** A mechanism used by Avalonia to automatically find and render the correct `View` for a given `ViewModel`.

### 3. `swgoh-command-bridge.Tests`
*   **Purpose:** Contains unit and integration tests for the `Core` project.
*   **Framework:** Uses **xUnit** as the testing framework.
*   **Scope:** Tests cover operation states, settings and transfer validation, cache migration/recovery, player profile parsing and repository sync, diagnostics, UI view-model workflows, mod filtering/mechanics/advisor decisions, assignment planning, and recommendation parsing/scraping.

## External Dependencies

*   **`swgoh-comlink`:** This is the critical read-only account service. On Windows x64, `ComlinkRuntimeManager` checks for a healthy configured local endpoint, otherwise downloads a pinned compatible release (`4.4.0`, then `4.2.0`), starts it as a hidden child process, waits for its local HTTP endpoint, and stops processes owned by the app. A healthy externally managed local or remote endpoint remains supported through the configurable URL; Linux and macOS currently require that arrangement. The application communicates with Comlink via HTTP from the `Core` project.
*   **`swgoh.gg`:** The application scrapes public-facing `swgoh.gg` "best mods" pages for supplemental data such as optimal mod sets, primary stats, and usage percentages. These calls originate from `SwgohGgScraperService` within the `Core` project and are cached locally.
*   **Preferred-mod dataset:** A maintainer-run local publisher queries local ComLink for high-ranking GAC profiles, aggregates only character set/primary usage and quality distributions, and commits a versioned dataset plus manifest to GitHub. Desktop clients download only those aggregate files, validate hashes, cache them atomically, and retain the embedded or last-known-good copy offline.

## Data Flow
There are two primary data flows:

**1. Player Data Sync:**
1.  A user action in the **View** (e.g., clicking "Fetch My Mods") triggers a `Command` in the corresponding **ViewModel**.
2.  The **ViewModel** calls a service method in the **Core** project.
3.  The composition root ensures the configured Comlink endpoint is reachable. On supported Windows x64 installs this may include starting the managed runtime; otherwise the configured external service must already be available. `PlayerService` then makes an HTTP call to Comlink.
4.  `swgoh-comlink` communicates with the official game servers.
5.  The service receives the raw JSON, optionally reads the Comlink metadata catalog, maps tolerant profile/roster/inventory variants and nested/display-name metadata through `PlayerProfileParser`, isolates malformed equipped/inventory records, and then caches the usable Core models in SQLite through `PlayerRepository`. Metadata failure is recorded as a warning and does not discard the primary roster payload.
6.  The service returns cached models to the **ViewModel**.
7.  The **ViewModel** updates its properties, and through data binding, the **View** automatically updates to display the new information.

**2. `swgoh.gg` Data Scraping:**
1.  On a user-triggered command, the `SwgohGgScraperService` is invoked with progress reporting and cooperative cancellation.
2.  The service reads cached roster characters from SQLite and processes them sequentially; the optimizer passes the active ally code for account-scoped refreshes, and the shell can switch among cached accounts without contacting Comlink.
3.  For each character without fresh cached data, the service makes an identifying HTTP request to the corresponding `swgoh.gg` "best mods" page, optionally includes configured contact metadata, retries transient failures, backs off on rate limits, and rejects responses over the bounded 2 MiB page limit.
4.  The parser extracts recommended mod sets and primary stats from the page HTML.
5.  The extracted data is stored in the local SQLite database as JSON fields on `SwgohGgRecommendationEntity`, overwriting old stale data for that character.
6.  The optimizer viewmodel reads the cached recommendation data and displays target sets, target primaries, popularity, last-scraped time, missing-data state, loadout completeness, assignment explanations, and the calculated loadout.

**3. Local recovery and account scope:**
1.  `AppDataPaths` resolves the shared platform-local application directory for the SQLite cache, JSON settings, diagnostics, cache backups, and (on managed Windows installs) versioned Comlink binaries.
2.  `CacheSchemaMigrator` creates or repairs the required SQLite tables and columns inside a transaction, normalizes known legacy mod-primary pairs, migrates legacy priorities, adds character alignment and relic tiers, and records the supported schema version (currently 11).
3.  Settings can create an integrity-checked backup, restore only a backup from the cache backup directory, or reset cached feature data while preserving JSON settings. Unsupported future-schema backups are rejected before replacement.
4.  `PlayerRepository` replaces one ally-code cache transactionally and deletes that account's character/mod rows transactionally. ViewModels always query the selected ally-code scope; cached-account switching is offline and never triggers a live sync. Startup uses the same selected ally-code scope to initiate at most one background refresh when the active cache is stale; failures preserve the previous cache and are surfaced through sync status.

**4. Preferred GAC mod data:**
1. A maintainer runs the publisher locally against local ComLink to query a few hundred high-ranking GAC accounts.
2. The publisher aggregates equipped mod sets and slot primaries by character, classifies clear preferences and viable alternatives, and writes no raw player payloads or ally codes to the repository.
3. The publisher validates the dataset and manifest before the maintainer commits `data/preferred-mods/` to GitHub.
4. At desktop startup, `PreferredModsDatasetService` loads the embedded baseline or cached `preferred-mods/current.json`, then silently checks the manifest at a bounded interval. Hash/schema validation and atomic replacement protect the last-known-good copy.
5. `CharactersViewModel` presents complete preferred setups and per-slot primary guidance, including advice for empty slots and tolerant wording for close usage splits.
