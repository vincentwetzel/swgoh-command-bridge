# State Flow

This document describes the application's state management strategy.

## Current State
The application currently follows a simple state management approach where each feature's state is managed by its corresponding ViewModel. Character, mod inventory, priority, threshold, and optimizer state are separated into their own ViewModels.

-   **Data:** Fetched from services in the `Core` layer and, for current UI screens, queried directly from the local EF Core/SQLite cache where needed.
-   **State:** Held in properties on the ViewModels.
-   **UI Updates:** Handled automatically by Avalonia's data binding system whenever ViewModel properties change (via `INotifyPropertyChanged`).
-   **Persistence:** `AppDbContext` stores players, characters, mods, and `swgoh.gg` recommendation JSON in the local SQLite cache. The EF Core files currently live at the repository root under Core namespaces and are planned for consolidation into `src/swgoh-command-bridge.Core`.

## Feature State
-   **Characters:** `CharactersViewModel` loads cached characters, applies search text, and orders by priority then name.
-   **Character Priorities:** `CharacterPrioritiesViewModel` loads cached characters, mirrors the selected character priority into an editable property, and persists priority changes through `AppDbContext`.
-   **Mods:** `ModsViewModel` maintains an in-memory mod list, applies rarity/sort filters, and asks `IModAdvisorService` for the selected mod recommendation.
-   **Optimizer:** `ModOptimizerViewModel` loads cached characters, reads cached `swgoh.gg` recommendation JSON for the selected character, exposes target sets/primaries, and calculates the recommended loadout asynchronously.

## Future Considerations
If the application's state becomes more complex and needs to be shared across multiple, disconnected ViewModels, a more centralized state management solution might be considered, such as:

-   A singleton "Application State" service.
-   A message bus (e.g., using `CommunityToolkit.Mvvm.IMessenger`).
-   A state management library like Redux.NET.
