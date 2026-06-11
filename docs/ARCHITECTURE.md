# Architecture Overview

This project follows a clean, decoupled architecture designed for a cross-platform desktop application using the Model-View-ViewModel (MVVM) pattern.

## Projects

The solution is divided into three main projects. Several root-level `.cs` files still exist as EF Core, repository, and Comlink integration drafts and should be consolidated into the relevant project before they are considered production code.

### 1. `swgoh-command-bridge.Core`
*   **Purpose:** This is the business logic and data layer of the application. It is a standard .NET library with no dependencies on any UI framework.
*   **Contents:**
    *   **Models:** Plain C# record types (`GameMod`, `Character`, etc.) that represent the core data entities of the application.
    *   **Services:** Classes responsible for fetching, caching, and manipulating data. The compiled project includes player, advisor, assignment, and `SwgohGgScraperService` scaffolds. Root-level drafts still cover Comlink access, EF Core persistence, repositories, and older service copies.
    *   **Data:** EF Core and SQLite are referenced by the Core project. `AppDbContext` and entity drafts currently live at the repository root under Core namespaces, with `SwgohGgRecommendationEntity` storing JSON payloads for recommended sets and slot primary stats. The intended local SQLite database stores cached player data and scraped mod recommendations.

### 2. `swgoh-command-bridge.UI`
*   **Purpose:** The presentation layer, built with **Avalonia UI**. This project is responsible for everything the user sees and interacts with.
*   **Pattern:** It strictly follows the **MVVM** pattern to ensure a clean separation between the UI (the "View") and the application logic (the "ViewModel").
*   **Contents:**
    *   **Views:** `.axaml` files that define the UI layout and controls. The code-behind (`.axaml.cs`) is kept minimal.
    *   **ViewModels:** Classes that expose data from the `Core` models to the `Views` and handle user commands. Character and priority viewmodels query SQLite-backed character data, the mods viewmodel filters/sorts mod inventory and evaluates selected mods, and the optimizer viewmodel displays scraped community recommendation context alongside a recommended loadout.
    *   **ViewLocator:** A mechanism used by Avalonia to automatically find and render the correct `View` for a given `ViewModel`.

### 3. `swgoh-command-bridge.Tests`
*   **Purpose:** Contains unit and integration tests for the `Core` project.
*   **Framework:** Uses **xUnit** as the testing framework.
*   **Scope:** Tests focus on verifying the correctness of the business logic in the services and models, ensuring data is processed correctly.

## External Dependencies

*   **`swgoh-comlink`:** This is a critical external service, expected to be running in a local Docker container. The application communicates with it via HTTP requests (from the `Core` project) to perform **read-only** data synchronization with the live game account. This avoids the need for the application to handle the complex game authentication and protocol itself.
*   **`swgoh.gg`:** The application will scrape the public-facing `swgoh.gg` website for supplemental data (e.g., optimal mod sets, primary stats for characters). These calls will originate from the `SwgohGgScraperService` within the `Core` project.

## Data Flow
There are two primary data flows:

**1. Player Data Sync:**
1.  A user action in the **View** (e.g., clicking "Fetch My Mods") triggers a `Command` in the corresponding **ViewModel**.
2.  The **ViewModel** calls a service method in the **Core** project.
3.  The service class in **Core** makes an HTTP call to the local `swgoh-comlink` instance.
4.  `swgoh-comlink` communicates with the official game servers.
5.  The service receives the data, maps it to the **Core** models (`GameMod`, `Character`), and potentially caches it in a local SQLite database.
6.  The service returns the models to the **ViewModel**.
7.  The **ViewModel** updates its properties, and through data binding, the **View** automatically updates to display the new information.

**2. `swgoh.gg` Data Scraping:**
1.  On application startup or on a user-triggered command, the `SwgohGgScraperService` is invoked.
2.  The service reads cached roster characters from SQLite and processes them sequentially.
3.  For each character, the service makes an HTTP request to the corresponding `swgoh.gg` "best mods" page, retries transient failures, and backs off on rate limits.
4.  The parser extracts recommended mod sets and primary stats from the page HTML.
5.  The extracted data is stored in the local SQLite database as JSON fields on `SwgohGgRecommendationEntity`, overwriting the old data for that character.
6.  The optimizer viewmodel reads the cached recommendation data and displays target sets, target primaries, popularity, and last-scraped time alongside the calculated mod loadout.
