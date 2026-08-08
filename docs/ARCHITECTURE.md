# Architecture Overview

This project follows a clean, decoupled architecture designed for a cross-platform desktop application using the Model-View-ViewModel (MVVM) pattern.

The active implementation is compiled from `src/`. Root-level C# files are historical drafts and are not project inputs. The desktop composition root owns one service graph for the process lifetime; there is no scheduled sync or hosted worker.

## Projects

The solution is divided into three main projects. A few older root-level `.cs` drafts still exist for reference, but active application code is compiled from the `src/` project directories.

### 1. `swgoh-command-bridge.Core`
*   **Purpose:** This is the business logic and data layer of the application. It is a standard .NET library with no dependencies on any UI framework.
*   **Contents:**
*   **Models:** Plain C# record types (`GameMod`, `Character`, `PlayerProfile`, etc.) plus small state/configuration records such as `OperationState<T>`, `AppSettings`, shared application-data paths, ally-code validation, player-sync diagnostics, and scraper progress models. Cached player entities also retain the last successful sync timestamp so offline freshness is visible.
*   **Services:** Classes responsible for fetching, caching, filtering, analyzing, and assigning data. This includes Comlink access, privacy-safe `ComlinkErrorFormatter` failure classification, tolerant `PlayerProfileParser` mapping with optional `CharacterMetadataParser` name enrichment, player sync with bounded durable outcome history, settings persistence and secret-safe transfer, threshold transfer, mod filtering/mechanics, mod upgrade advice, priority-first roster assignment planning, and `swgoh.gg` scraping. `PlayerRepository` owns transactional replacement and account-cache deletion so UI account management does not implement persistence rules itself.
    *   **Data:** EF Core and SQLite are compiled in the Core project. `AppDbContext`, database entities, and repositories live under `src/swgoh-command-bridge.Core/Database`. `SwgohGgRecommendationEntity` stores JSON payloads for recommended sets and slot primary stats under a composite character/ally-code key, while player, character, and mod entities cache synced account data.

### 2. `swgoh-command-bridge.UI`
*   **Purpose:** The presentation layer, built with **Avalonia UI**. This project is responsible for everything the user sees and interacts with.
*   **Pattern:** It strictly follows the **MVVM** pattern to ensure a clean separation between the UI (the "View") and the application logic (the "ViewModel").
*   **Contents:**
    *   **Views:** `.axaml` files that define the UI layout and controls. The code-behind (`.axaml.cs`) is kept minimal.
*   **ViewModels:** Classes that expose data from the `Core` models to the `Views` and handle user commands. `StateViewModelBase<T>` centralizes loading/empty/success/error projections and transition notifications, while feature viewmodels retain specialized flags for filtering, scraping, selections, and status text. The main window lists cached accounts, reports active character/mod counts, and switches the active ally-code scope offline; character and priority viewmodels query SQLite-backed character data, the mods viewmodel filters, deterministically sorts, pages, labels mod inventory, renders readable stat details and advisor efficiency estimates, and guards asynchronous advisor results against stale selections, and the optimizer viewmodel displays scraped community recommendation context alongside selected-character and priority-roster loadouts. Settings explicitly controls whether new local `swgoh.gg` scraping requests are allowed; cached recommendations remain readable when that policy is disabled. Feature screens use explicit empty/loading/success/error state instead of preview fallback data.
*   **Composition:** `src/swgoh-command-bridge.UI/ApplicationComposition.cs` is the desktop composition root. It creates the shared database, settings, Comlink client/service, repositories, player service, advisor, assignment service, and scraper, while `App` disposes the owned database and HTTP resources when the main window closes. `AppDataPaths` provides one case-stable application-data directory for cache, settings, diagnostics, and backups; composition accepts injected settings and an optional player service for isolated shell and sync-lifecycle tests.
*   **Diagnostics:** `DiagnosticsViewModel` reads only local metadata, aggregate cache counts, and bounded sync outcome history, includes a bounded in-memory `DiagnosticEventLog` plus `DiagnosticLogger<T>` capture for Core service activity, and exports a privacy-redacted report without account payloads or credentials.
*   **Recommendation contract:** `SwgohGgRecommendationParser` converts page markup into a fixture-testable `SwgohGgRecommendationParseResult`; `RecommendationSnapshot` is then the shared boundary for persisted community data. The database retains the source, payload schema version, source URL, scrape time, set percentages, and per-slot primary recommendations for assignment and UI consumers. `ModLoadoutResult` carries completeness, set-rule validity, status, per-mod explanations, popularity-weighted lower-ranked alternatives, assignment-score deltas, equipped-mod swap candidates, and conservative persisted mod-stat projections back to the optimizer, while `RosterLoadoutResult` supports both priority-first and bounded joint coverage, plus consolidated swap candidates annotated with inventory availability, reservation, missing-slot, and invalid-set conflict context.
    *   **ViewLocator:** A mechanism used by Avalonia to automatically find and render the correct `View` for a given `ViewModel`.

### 3. `swgoh-command-bridge.Tests`
*   **Purpose:** Contains unit and integration tests for the `Core` project.
*   **Framework:** Uses **xUnit** as the testing framework.
*   **Scope:** Tests cover operation states, settings and transfer validation, cache migration/recovery, player profile parsing and repository sync, diagnostics, UI view-model workflows, mod filtering/mechanics/advisor decisions, assignment planning, and recommendation parsing/scraping.

## External Dependencies

*   **`swgoh-comlink`:** This is a critical external service, expected to be running in a local Docker container. The application communicates with it via configurable HTTP requests from the `Core` project to perform **read-only** data synchronization with the live game account. This avoids the need for the application to handle the complex game authentication and protocol itself.
*   **`swgoh.gg`:** The application scrapes public-facing `swgoh.gg` "best mods" pages for supplemental data such as optimal mod sets, primary stats, and usage percentages. These calls originate from `SwgohGgScraperService` within the `Core` project and are cached locally.

## Data Flow
There are two primary data flows:

**1. Player Data Sync:**
1.  A user action in the **View** (e.g., clicking "Fetch My Mods") triggers a `Command` in the corresponding **ViewModel**.
2.  The **ViewModel** calls a service method in the **Core** project.
3.  `PlayerService` makes an HTTP call to the configured local `swgoh-comlink` instance.
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
1.  `AppDataPaths` resolves the shared platform-local application directory for the SQLite cache, JSON settings, diagnostics, and cache backups.
2.  `CacheSchemaMigrator` creates or repairs the required SQLite tables and columns inside a transaction, then records the supported schema version.
3.  Settings can create an integrity-checked backup, restore only a backup from the cache backup directory, or reset cached feature data while preserving JSON settings. Unsupported future-schema backups are rejected before replacement.
4.  `PlayerRepository` replaces one ally-code cache transactionally and deletes that account's character/mod rows transactionally. ViewModels always query the selected ally-code scope; cached-account switching is offline and never triggers a live sync.
