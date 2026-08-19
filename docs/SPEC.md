# Project Specification

## 1. Core Purpose
The **SWGOH Command Bridge** is a cross-platform desktop application designed to provide advanced, in-depth mod analysis and optimization recommendations for the mobile game *Star Wars: Galaxy of Heroes* (SWGOH). It serves as a powerful tool for players to review their inventory and plan optimal character loadouts based on data-driven analysis.

## 2. Key Features

The current release target is read-only. Any feature that changes the game account is explicitly out of scope for the active product. Local cache, settings, backups, diagnostics, and recommendation requests are separate local/public-data operations and do not modify the game account.

### 2.1. Account & Data Management
-   **Read-Only Account Sync:** The application interfaces with a local `swgoh-comlink` instance to perform a **read-only** sync of the user's game account.
-   **Full Roster Sync:** It fetches, parses, persists, and displays the usable character and mod inventory records returned by Comlink, preserving warnings for tolerated malformed records.
-   **Local Caching:** It uses a local SQLite database to cache player data, minimizing redundant API calls and enabling offline viewing.
-   **Configurable Local Services:** It stores the local `swgoh-comlink` base URL, default ally code, normalized Dark/Light/System theme choice, scraping policy, and user-defined mod thresholds in cross-platform application settings.
-   **Character Catalog:** It ships verified embedded character/ship catalogs, best-effort refreshes them from Comlink, resolves localized names and portrait assets, and atomically preserves the previous snapshot when a refresh is rejected.

### 2.2. Mod Viewing & Filtering
-   **Inventory View:** Display the entire mod inventory in a sortable, filterable grid.
-   **Advanced Filtering:** Filter mods by any combination of slot, set, primary stat, secondary stats, level, pips, tier, and equipped status.
-   **Quick Search:** Instantly find mods with specific secondary stats (e.g., "all mods with Speed secondaries").
-   **Explicit UI States:** Feature views should distinguish loading, empty, success, and error states without relying on mock fallback data.

### 2.3. Mod Analysis & Optimization
-   **Recommendation Engine:**
    -   **Scrape `swgoh.gg` Data:** The application scrapes `swgoh.gg`'s "best mods" pages for each character to gather data-driven recommendations. This data includes popular mod sets and primary stats with their usage percentages.
    -   **Local Database Caching:** Scraped data is stored in the local SQLite database. To avoid overwhelming `swgoh.gg`, scraping is sequential and incremental, with progress, cancellation, failure reporting, and stale-data checks. A central shared recommendation service remains future scope.
    -   **Flexible Recommendations:** The engine does not recommend only the single best mod set or primary. It considers competitive alternatives based on scraped percentages, especially when the user has a high-quality mod of that type available.
    -   **Prioritize Roster Coverage:** Ensure that the recommendation engine prioritizes equipping *some* mod on all active characters over leaving them unmodded, even if the available mods are of lower quality (e.g., poor stats, unupgraded). A sub-optimal mod that matches the character's desired set or primary stat is considered better than no mod at all. This ensures maximum roster coverage.
-   **Mod Swap Suggestions:** Generate a list of recommended mod swaps (e.g., move mod X from Character A to Character B) to optimize a character or squad.
-   **Upgrade Planning:** Identify and suggest which mods are the best candidates for upgrading or slicing to achieve better stats.
-   **Assignment Explanations:** Explain each recommended assignment using set match, primary match, Speed value, source character, and expected benefit. When current equipped mods are available, show persisted mod-stat deltas separately from the score estimate; do not present them as final in-game stats.
-   **Roster Planning:** Provide a deterministic priority-first plan and a bounded joint plan for multiple characters. Each plan reserves an inventory mod at most once and reports missing slots, conflicts, reservation context, and consolidated swap candidates.

### 2.3.1. Mod Upgrade & Replacement Advisor
-   **User-Defined Thresholds:** Users can define rules for when a mod is considered "worth upgrading." The active advisor rules use the mod's pips, tier, Speed, compatible ownership, and optional secondary-roll efficiency estimate; the advisor exposes current and projected efficiency with an explicit analysis-only disclaimer. For a detailed explanation of the mod upgrading process, see [`MOD_MECHANICS.md`](./MOD_MECHANICS.md).
-   **Upgrade/Swap/Sell Logic:** Based on these thresholds, the system provides recommendations for each mod. It evaluates upgrade and slice potential first, then considers a faster compatible swap against prioritized equipped characters, and finally returns sell when no actionable recommendation applies. These are recommendations only; no game action is performed.

### 2.4. User Experience
-   **Desktop targets:** Avalonia and the Core library are intended for Windows, macOS, and Linux. Windows x64 is the primary verified target and the only target with automatic Comlink installation; other platforms require an externally managed Comlink endpoint until their release artifacts and smoke tests are verified.
-   **Responsive UI:** The user interface should be clean, intuitive, and responsive, capable of handling and displaying large amounts of data without performance degradation.

### 2.5. Recovery and support
-   **Cache recovery:** Users can create verified SQLite backups, restore a selected backup, or reset cached feature data while retaining JSON settings.
-   **Diagnostics:** Users can inspect safe cache/configuration metadata and export a privacy-redacted support report.
-   **Sync history:** The cache retains bounded, privacy-safe outcomes for completed, failed, cancelled, and interrupted account sync attempts.
-   **Failure states:** Screens must expose loading, empty, success, and error states with retry or recovery actions where applicable.

## 3. Stretch Goals (Future Scope)
-   **Secure Write-Access Login**: Authenticate with the user's account with permissions to make changes.
-   **Automated Equipping**: Execute suggested mod swaps with a single action.
-   **Bulk Mod Upgrading**: Perform batch upgrading and slicing of mods directly from the application.

## 4. Technical Stack
-   **Language:** C#
-   **Framework:** .NET 8
-   **UI:** Avalonia UI
-   **Architecture:** Model-View-ViewModel (MVVM)
-   **Local Database:** SQLite (via Entity Framework Core)
-   **Game API:** `swgoh-comlink` (via configured HTTP calls; Windows x64 can manage the default local runtime)
-   **Community Data:** Public `swgoh.gg` pages via remote HTTP calls and HTML parsing

The compiled implementation lives under `src/`. The application is composed as a desktop process with no scheduled background worker. On startup, after cached data is available, the selected account is refreshed once in the background when its cache is older than the freshness threshold, and the catalog may perform one best-effort refresh; explicit sync and recommendation refreshes remain user-triggered. On supported Windows x64 startup, the composition root may own a downloaded Comlink child process for the lifetime of the desktop process.

## 5. Data and privacy boundary
- Account payloads and recommendation cache data remain local unless the user separately operates the configured Comlink or public recommendation requests.
- Logs and diagnostics must not contain full ally codes, access keys, session values, or raw account payloads.
- The application may request public `swgoh.gg` recommendation pages, subject to the service's rate limits and freshness policy.
- The local scraping switch prevents new recommendation requests while retaining previously cached recommendations for offline viewing.
