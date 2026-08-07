# State Flow

This document describes the application's state management strategy and the boundaries between live requests, local cache state, and ViewModel state.

## Current State
The application currently follows a simple state management approach where each feature's state is managed by its corresponding ViewModel. Character, mod inventory, priority, threshold, and optimizer state are separated into their own ViewModels.

-   **Startup:** `MainWindowViewModel` keeps cache initialization failures in a visible retryable state so the shell and Settings screen can still open while the local database issue is addressed.

-   **Composition:** `ApplicationComposition` creates the shared service graph once for the desktop lifetime. The main window owns that composition and disposes its HTTP client and default database when the window closes.

-   **Data:** Fetched from services in the `Core` layer and, for current UI screens, queried from the local EF Core/SQLite cache where needed. Comlink profiles pass through `PlayerProfileParser`, which preserves usable records when individual fields or mod entries are malformed.
-   **State:** Held in properties on the ViewModels, with `OperationState<T>` used where screens need explicit empty, loading, success, and error status.
-   **UI Updates:** Handled automatically by Avalonia's data binding system whenever ViewModel properties change (via `INotifyPropertyChanged`).
-   **Persistence:** `AppDbContext` stores players, characters, mods, and `swgoh.gg` recommendation JSON in the local SQLite cache. EF Core context, entities, repositories, and the transactional `CacheSchemaMigrator` live under `src/swgoh-command-bridge.Core/Database`; SQLite caches record a supported schema version and expose the most recent migration result while the full EF migration history is pending. Settings can create and integrity-check restore timestamped SQLite backups and includes confirmation-guarded reset/restore actions that preserve JSON application settings. `SettingsService` stores application configuration as JSON in the user's local application data folder using atomic writes.

-   **Diagnostics:** `DiagnosticsViewModel` reports cache reachability, aggregate record counts, safe configuration metadata, and the persisted scraper summary. Its export omits full ally codes, account payloads, access keys, and session values.

## Feature State
-   **Characters:** `CharactersViewModel` loads cached characters for the active ally code, applies search text, orders by priority then name, and exposes explicit non-preview UI state.
-   **Character Priorities:** `CharacterPrioritiesViewModel` loads cached characters for the active ally code, mirrors the selected character priority into an editable property, validates the 0–100 range, exposes dirty-state cancel behavior, and persists priority changes through `AppDbContext`.
-   **Thresholds:** `ModThresholdsViewModel` loads backward-compatible threshold settings, preserves stable IDs, supports duplicate/edit/delete flows, validates values before saving or importing, persists the selected default threshold, and exports/imports a versioned JSON threshold document.
-   **Mods:** `ModsViewModel` loads the SQLite inventory and cached character/equipped-mod context into memory, projects roster names onto mod rows, applies the screen's slot, set, primary, comma-separated secondary-stat combination/value, minimum-level, tier, pips, equipped, and deterministic sort filters, pages matching results in 100-item windows, shows the active persisted advisor threshold, then asks `IModAdvisorService` for the selected mod recommendation.
-   **Optimizer:** `ModOptimizerViewModel` loads cached characters and mods for the active ally code, reads the shared `RecommendationSnapshot` for the selected character, identifies current versus seven-day-stale or missing community data, exposes target sets/primaries, consumes `ModLoadoutResult` for completeness, set-rule validity, status, and per-mod explanations, and can request a priority-first roster plan that reserves each mod once and reports conflicts.
-   **Scraping:** `SwgohGgScraperService` reports `ScrapeProgress`, delegates markup handling to `SwgohGgRecommendationParser`, supports selected-character and active-ally-code-scoped all-character refreshes with cancellation, skips fresh recommendations, records failures without requiring a scheduled background worker, and the optimizer persists the last refresh summary in application settings.

## Operation lifecycle

User commands follow this pattern: validate input, enter a loading state, perform a cancellable Core operation, persist successful results, refresh affected ViewModels, and finish in success, empty, cancelled, or error state. Cancellation is not treated as a successful refresh. Existing valid cache data remains available when a later request fails or is cancelled.

## Future Considerations
If the application's state becomes more complex and needs to be shared across multiple, disconnected ViewModels, a more centralized state management solution might be considered, such as:

-   A singleton "Application State" service.
-   A message bus (e.g., using `CommunityToolkit.Mvvm.IMessenger`).
-   A state management library like Redux.NET.
