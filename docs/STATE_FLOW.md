# State Flow

This document describes the application's state management strategy.

## Current State
The application currently follows a simple state management approach where each feature's state is managed by its corresponding ViewModel. The current UI scaffolds separate character, mod inventory, priority, threshold, and optimizer state into their own ViewModels.

-   **Data:** Fetched from services in the `Core` layer.
-   **State:** Held in properties on the ViewModels.
-   **UI Updates:** Handled automatically by Avalonia's data binding system whenever ViewModel properties change (via `INotifyPropertyChanged`).
-   **Persistence:** Intended to flow through the Core persistence layer once the root-level EF Core drafts are moved into the compiled project.

## Future Considerations
If the application's state becomes more complex and needs to be shared across multiple, disconnected ViewModels, a more centralized state management solution might be considered, such as:

-   A singleton "Application State" service.
-   A message bus (e.g., using `CommunityToolkit.Mvvm.IMessenger`).
-   A state management library like Redux.NET.
