# Project Specification

## 1. Core Purpose
The **SWGOH Command Bridge** is a cross-platform desktop application designed to provide advanced, in-depth mod analysis and optimization recommendations for the mobile game *Star Wars: Galaxy of Heroes* (SWGOH). It serves as a powerful tool for players to review their inventory and plan optimal character loadouts based on data-driven analysis.

## 2. Key Features

### 2.1. Account & Data Management
-   **Read-Only Account Sync:** The application will interface with a local `swgoh-comlink` instance to perform a **read-only** sync of the user's game account.
-   **Full Roster Sync:** Fetch, parse, persist, and display a user's complete character and mod inventory for analysis.
-   **Local Caching:** Utilize a local SQLite database to cache player data, minimizing redundant API calls and enabling offline viewing.
-   **Configurable Local Services:** Store the local `swgoh-comlink` base URL and user-defined mod thresholds in cross-platform application settings.

### 2.2. Mod Viewing & Filtering
-   **Inventory View:** Display the entire mod inventory in a sortable, filterable grid.
-   **Advanced Filtering:** Filter mods by any combination of slot, set, primary stat, secondary stats, level, pips, tier, and equipped status.
-   **Quick Search:** Instantly find mods with specific secondary stats (e.g., "all mods with Speed secondaries").
-   **Explicit UI States:** Feature views should distinguish loading, empty, success, and error states without relying on mock fallback data.

### 2.3. Mod Analysis & Optimization
-   **Recommendation Engine:**
    -   **Scrape `swgoh.gg` Data:** The application will scrape `swgoh.gg`'s "best mods" pages for each character to gather data-driven recommendations. This data includes popular mod sets and primary stats with their usage percentages.
    -   **Local Database Caching:** The scraped data will be stored in the local SQLite database. To avoid overwhelming `swgoh.gg`, the scraping will be done slowly and incrementally, with progress, cancellation, failure reporting, and stale-data checks. Future scope: replace local scraping with a central shared recommendation service.
    -   **Flexible Recommendations:** The engine will not just recommend the single best mod set or primary. It will consider highly competitive alternatives based on the scraped percentages. For example, if a primary stat has high usage (e.g., 45%), it will be considered a valid alternative to the most popular option (e.g., 55%), especially if the user has a high-quality mod of that type available.
    -   **Prioritize Roster Coverage:** Ensure that the recommendation engine prioritizes equipping *some* mod on all active characters over leaving them unmodded, even if the available mods are of lower quality (e.g., poor stats, unupgraded). A sub-optimal mod that matches the character's desired set or primary stat is considered better than no mod at all. This ensures maximum roster coverage.
-   **Mod Swap Suggestions:** Generate a list of recommended mod swaps (e.g., move mod X from Character A to Character B) to optimize a character or squad.
-   **Upgrade Planning:** Identify and suggest which mods are the best candidates for upgrading or slicing to achieve better stats.
-   **Assignment Explanations:** Explain each recommended assignment using set match, primary match, Speed value, source character, and expected benefit.

### 2.3.1. Mod Upgrade & Replacement Advisor
-   **User-Defined Thresholds:** The application will allow users to define a set of rules for when a mod is considered "worth upgrading." These rules will be based on the mod's quality (color), level, and the value of its secondary stats (e.g., "a green mod is worth upgrading if it has at least 8 Speed"). For a detailed explanation of the mod upgrading process, see [`MOD_MECHANICS.md`](./MOD_MECHANICS.md).
-   **Upgrade/Swap/Sell Logic:** Based on these thresholds, the application will provide recommendations for each mod:
    -   **Upgrade:** If a mod meets the user's criteria, the recommendation will be to continue upgrading it.
    -   **Swap:** If a mod is no longer worth upgrading, the system will compare it to mods already equipped on characters. If the mod has the same set and primary stat as an equipped mod but has a higher Speed secondary, it will recommend a swap. This logic will prioritize checking for swap opportunities on high-priority characters first.
    -   **Sell:** If a mod is not worth upgrading and is not a good candidate for a swap (i.e., its speed is lower than all assigned mods of the same set and primary), the recommendation will be to sell it.

### 2.4. User Experience
-   **Cross-Platform:** The application must run natively on Windows, macOS, and Linux.
-   **Responsive UI:** The user interface should be clean, intuitive, and responsive, capable of handling and displaying large amounts of data without performance degradation.

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
-   **Game API:** `swgoh-comlink` (via local HTTP calls)
-   **Community Data:** Public `swgoh.gg` pages via remote HTTP calls and HTML parsing
