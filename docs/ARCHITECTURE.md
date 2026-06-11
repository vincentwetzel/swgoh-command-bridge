# Architecture Overview

This project follows a clean, decoupled architecture designed for a cross-platform desktop application using the Model-View-ViewModel (MVVM) pattern.

## Projects

The solution is divided into three main projects. Several root-level `.cs` files currently exist as integration drafts and should be moved into the relevant project before they are considered production code.

### 1. `swgoh-command-bridge.Core`
*   **Purpose:** This is the business logic and data layer of the application. It is a standard .NET library with no dependencies on any UI framework.
*   **Contents:**
    *   **Models:** Plain C# record types (`GameMod`, `Character`, etc.) that represent the core data entities of the application.
    *   **Services:** Classes responsible for fetching, caching, and manipulating data. The compiled project currently includes early player, advisor, and assignment service scaffolds. Root-level drafts cover Comlink access, `swgoh.gg` scraping, EF Core persistence, and repositories.
    *   **Data:** EF Core and SQLite are referenced by the Core project, while the current `AppDbContext` and entity drafts still live at the repository root. The intended local SQLite database stores cached player data and scraped mod recommendations.

### 2. `swgoh-command-bridge.UI`
*   **Purpose:** The presentation layer, built with **Avalonia UI**. This project is responsible for everything the user sees and interacts with.
*   **Pattern:** It strictly follows the **MVVM** pattern to ensure a clean separation between the UI (the "View") and the application logic (the "ViewModel").
*   **Contents:**
    *   **Views:** `.axaml` files that define the UI layout and controls. The code-behind (`.axaml.cs`) is kept minimal.
    *   **ViewModels:** Classes that expose data from the `Core` models to the `Views` and handle user commands. Current scaffolds include characters, mods, priorities, thresholds, and optimizer views.
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
2.  The service checks the local database for the last scraped time for each character.
3.  If a character's data is stale, the service makes an HTTP request to the corresponding `swgoh.gg` "best mods" page.
4.  The intended parser extracts recommended mod sets and primary stats, including usage percentages. The current root-level draft still uses placeholder extraction.
5.  The extracted data is stored in the local SQLite database, overwriting the old data for that character.
6.  Other services, like the `ModAdvisorService`, can then query this data from the database to inform their recommendations.
