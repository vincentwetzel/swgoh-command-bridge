# Architecture Overview

This project follows a clean, decoupled architecture designed for a cross-platform desktop application using the Model-View-ViewModel (MVVM) pattern.

## Projects

The solution is divided into three main projects. A few older root-level `.cs` drafts still exist for reference, but active application code is compiled from the `src/` project directories.

### 1. `swgoh-command-bridge.Core`
*   **Purpose:** This is the business logic and data layer of the application. It is a standard .NET library with no dependencies on any UI framework.
*   **Contents:**
    *   **Models:** Plain C# record types (`GameMod`, `Character`, `PlayerProfile`, etc.) plus small state/configuration records such as `OperationState<T>`, `AppSettings`, and scraper progress models.
    *   **Services:** Classes responsible for fetching, caching, filtering, analyzing, and assigning data. This includes Comlink access, player sync, settings persistence, mod filtering/mechanics, mod upgrade advice, roster assignment planning, and `swgoh.gg` scraping.
    *   **Data:** EF Core and SQLite are compiled in the Core project. `AppDbContext`, database entities, and `PlayerRepository` live under `src/swgoh-command-bridge.Core/Database`. `SwgohGgRecommendationEntity` stores JSON payloads for recommended sets and slot primary stats, while player, character, and mod entities cache synced account data.

### 2. `swgoh-command-bridge.UI`
*   **Purpose:** The presentation layer, built with **Avalonia UI**. This project is responsible for everything the user sees and interacts with.
*   **Pattern:** It strictly follows the **MVVM** pattern to ensure a clean separation between the UI (the "View") and the application logic (the "ViewModel").
*   **Contents:**
    *   **Views:** `.axaml` files that define the UI layout and controls. The code-behind (`.axaml.cs`) is kept minimal.
    *   **ViewModels:** Classes that expose data from the `Core` models to the `Views` and handle user commands. Character and priority viewmodels query SQLite-backed character data, the mods viewmodel filters/sorts mod inventory and evaluates selected mods, and the optimizer viewmodel displays scraped community recommendation context alongside a recommended loadout. Feature screens use explicit empty/loading/success/error state instead of preview fallback data.
    *   **ViewLocator:** A mechanism used by Avalonia to automatically find and render the correct `View` for a given `ViewModel`.

### 3. `swgoh-command-bridge.Tests`
*   **Purpose:** Contains unit and integration tests for the `Core` project.
*   **Framework:** Uses **xUnit** as the testing framework.
*   **Scope:** Tests cover operation state behavior, settings load/save fallbacks, player profile parsing and repository sync, mod filtering, mod mechanics, and advisor recommendations.

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
5.  The service receives the raw JSON, recursively maps profile, roster unit, and equipped mod payloads to **Core** models and EF entities, then caches them in SQLite through `PlayerRepository`.
6.  The service returns cached models to the **ViewModel**.
7.  The **ViewModel** updates its properties, and through data binding, the **View** automatically updates to display the new information.

**2. `swgoh.gg` Data Scraping:**
1.  On a user-triggered command, the `SwgohGgScraperService` is invoked with progress reporting and cooperative cancellation.
2.  The service reads cached roster characters from SQLite and processes them sequentially.
3.  For each character without fresh cached data, the service makes an HTTP request to the corresponding `swgoh.gg` "best mods" page, retries transient failures, and backs off on rate limits.
4.  The parser extracts recommended mod sets and primary stats from the page HTML.
5.  The extracted data is stored in the local SQLite database as JSON fields on `SwgohGgRecommendationEntity`, overwriting old stale data for that character.
6.  The optimizer viewmodel reads the cached recommendation data and displays target sets, target primaries, popularity, last-scraped time, missing-data state, assignment explanations, and calculated swap recommendations.
