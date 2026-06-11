# Coding Standards

To ensure consistency and maintainability, all code contributed to this project should adhere to the following standards.

## 1. Naming Conventions
-   **Namespaces:** Use `PascalCase`, e.g., `swgoh_command_bridge.Core.Services`.
-   **Classes, Records, Enums, Interfaces, Methods, Properties:** Use `PascalCase`, e.g., `public class PlayerService`, `public record GameMod`.
-   **Interface Names:** Prefix with `I`, e.g., `IPlayerService`.
-   **Local Variables & Method Parameters:** Use `camelCase`, e.g., `var characterId = ...`.
-   **Private Fields:** Prefix with an underscore (`_`), e.g., `private readonly IPlayerService _playerService;`.

## 2. Code Style
-   **Braces:** Use Allman style braces (braces on a new line).
    ```csharp
    public class MyClass
    {
        public void MyMethod()
        {
            // ...
        }
    }
    ```
-   **`var` Keyword:** Use `var` for local variable declarations when the type is obvious from the right-hand side of the assignment. Otherwise, use the explicit type name for clarity.
-   **`using` Statements:** Place `using` statements at the top of the file. Remove and sort them for cleanliness.
-   **File Organization:** Each file should contain a single class, record, or enum.

## 3. Architecture & Patterns
-   **MVVM:** Adhere strictly to the Model-View-ViewModel pattern in the `UI` project.
    -   **Views:** Should contain minimal to no code-behind. Logic belongs in the ViewModel.
    -   **ViewModels:** Should not have any reference to `Avalonia` or any other UI framework. They should be pure C#.
    -   **Models:** Data structures should be defined in the `.Core` project.
-   **Dependency Injection:** Services should be injected into ViewModels and other services via constructors. This makes the code more modular and testable.
-   **Async/Await:** Use `async` and `await` for all I/O-bound operations (file access, database calls, HTTP requests).
    -   Asynchronous methods should have an `Async` suffix, e.g., `GetPlayerAsync`.
    -   Use `ConfigureAwait(false)` in the `Core` library to avoid deadlocks.

## 4. Documentation
-   **Public API:** All public classes, methods, and properties should have XML documentation comments explaining their purpose.
-   **Complex Logic:** Add comments to explain *why* a complex piece of logic is implemented a certain way, not *what* it does. The code itself should be self-explanatory.

## 5. Nullability & Modern C# Features
- **Nullable Reference Types:** Enable nullable annotations (`#nullable enable`) across all projects. Avoid using the null-forgiving operator (`!`) unless absolutely necessary (e.g., in unit testing setup or compiler-override edge cases).
- **Pattern Matching:** Prefer pattern matching and switch expressions over complex nested `if-else` statements for readability.
- **Immutability:** Use `record` or `readonly struct` for data transfer objects (DTOs) and models that do not require state changes after instantiation.

## 6. Error Handling & Logging
- **Avoid Swallowing Exceptions:** Never use empty `catch` blocks. If you catch an exception, either log it, wrap it in a custom exception, or rethrow it using `throw;` (to preserve the stack trace).
- **Defensive Programming:** Validate method arguments at public boundaries using standard guard clauses, e.g., `ArgumentNullException.ThrowIfNull(parameter)`.
- **Logging:** Inject an `ILogger<T>` instance into services instead of using standard output (`Console.WriteLine`) to ensure trace data is structured and redirectable.

## 7. Testing Standards
- **Pattern:** Use the **Arrange-Act-Assert (AAA)** structure inside test methods, separating each phase with a blank line.
- **Naming Convention:** Name test methods using the `UnitOfWork_StateUnderTest_ExpectedBehavior` format.
  ```csharp
  [Fact]
  public async Task GetPlayerAsync_WhenPlayerIdDoesNotExist_ThrowsKeyNotFoundException()
  {
      // Arrange
      // Act
      // Assert
  }
  ```

## 8. Complexity & File Sizing
- **Keep Files Compact:** Adhere to a soft rule of keeping source files below **300 lines of code (LOC)**.
- **Single Responsibility:** If a class begins exceeding 300 LOC, consider refactoring it by delegating sub-responsibilities to new helper classes, services, or sub-viewmodels.

## 9. UI Threading & MVVM Data Binding
- **UI Thread Safety:** Never run blocking, synchronous, or CPU-heavy tasks directly on the main UI thread. Offload them to background threads using `Task.Run` or asynchronous calls, and marshal UI updates back to the main thread using `Dispatcher.UIThread.Post` if required by the framework.
- **Commands Over Events:** Bind UI interactions (like button clicks) to ViewModel commands (e.g., using `IRelayCommand` or ReactiveUI's `ReactiveCommand`) rather than writing click handlers in the View's code-behind.
- **Property Notification:** Ensure properties in ViewModels that bind to the UI raise change notifications (e.g., using `RaiseAndSetIfChanged` or calling `OnPropertyChanged`).

## 10. Resource & Lifecycle Management
- **IDisposable Implementation:** Any class managing disposable resources (e.g., database connections, file streams, network sockets, timers) must implement `IDisposable` or `IAsyncDisposable`.
- **Using Declarations:** Prefer modern `using` declarations over nested `using` blocks to keep nesting depth low:
  ```csharp
  using var response = await _httpClient.GetAsync(url);
  ```
- **Event Unsubscriptions:** Always unsubscribe from events or use weak event patterns to prevent memory leaks when binding short-lived objects (Views/ViewModels) to long-lived services.

## 11. Network & API Communication
- **HttpClient Lifetime:** Never instantiate `HttpClient` directly via `new HttpClient()`. Use `IHttpClientFactory` or typed Dependency Injection clients to prevent socket exhaustion and respect DNS changes.
- **Resilience & Timeouts:** Always configure reasonable timeouts on outgoing HTTP calls. Implement exponential backoff retry policies (e.g., using Polly) for external API scraping to handle transient network issues cleanly without overloading the target servers.

## 12. Database (SQLite & EF Core) Guidelines
- **Asynchronous Operations:** Always use asynchronous EF Core APIs (e.g., `SaveChangesAsync`, `ToListAsync`, `FirstOrDefaultAsync`) to ensure the UI remains fully responsive during database operations.
- **No-Tracking for Read-Only Queries:** Use `.AsNoTracking()` for queries where the returned entities will not be modified. This improves performance and reduces memory consumption.
- **Migration Safety:** Ensure database schema changes are managed via explicit, versioned migrations. Apply pending migrations automatically on application startup only if safe, or handle them via an explicit initialization phase.

## 13. Security & Secrets Management
- **No Hardcoded Secrets:** Never hardcode API keys, developer credentials, or user-session identifiers in the source code. Use configuration files, environment variables, or platform-specific secure storage (such as the OS credential manager).
- **Sensitive Data Handling:** Avoid storing passwords or session tokens in plain-text strings in memory for longer than necessary; clear or zero-out sensitive byte arrays once they are processed.

## 14. Globalization & Culture-Safe Parsing
- **Invariant Culture:** When parsing numerical values, percentages, or dates from external APIs or scraped web pages (such as `swgoh.gg`), always specify `CultureInfo.InvariantCulture` (and `StringComparison.Ordinal` / `StringComparison.OrdinalIgnoreCase`). This avoids runtime parsing crashes or behavioral bugs on systems running non-US regional formats (e.g., European systems using commas `,` instead of periods `.` as decimal separators).
- **Internal Formatting:** Ensure logging, cache files, and telemetry output represent data in a culture-agnostic format unless directly rendering numbers to the UI for user presentation.

## 15. Rich Domain Design
- **Avoid Anemic Domain Models:** Keep domain entities rich. Calculation logic (e.g., computing final mod stat adjustments, determining if a mod is eligible for slicing, or scoring a mod's efficiency) must be encapsulated within the models in the `.Core` project (like `GameMod` or `Character`), or dedicated domain services. Keep this logic out of the UI ViewModels, converters, or views.

## 16. Cross-Platform File Paths & Storage
- **No Hardcoded Absolute Paths:** Never hardcode absolute file paths or system-specific separators (e.g., `C:\ProgramData\...`). Use `Environment.GetFolderPath` combined with `Environment.SpecialFolder.LocalApplicationData` to locate writeable paths securely on Windows, macOS, and Linux.
- **Path Manipulation:** Always use `Path.Combine` and `Path.DirectorySeparatorChar` rather than manual string concatenation to maintain path formatting consistency across operating systems.

## 17. Concurrency & Async Synchronization
- **Async-Safe Locks:** Do not use the synchronous `lock` statement around block sections that contain `await` expressions. Instead, utilize `SemaphoreSlim` for managing asynchronous execution access.
- **Guaranteed Release:** Always release synchronization primitives within a `finally` block to prevent terminal application deadlocks if an unexpected exception occurs:
  ```csharp
  await _semaphore.WaitAsync(cancellationToken);
  try
  {
      // Asynchronous critical-section execution
  }
  finally
  {
      _semaphore.Release();
  }
  ```

## 18. Performance Optimization & Allocation Management
- **Avoid LINQ in Hot Paths:** Refrain from heavy LINQ operations (e.g., `.Select`, `.Where`, `.Any`) inside intensive performance-critical loops, such as batch algorithms processing or scoring thousands of mods. Prefer traditional `foreach` or index-based `for` loops to reduce Heap allocation overhead.
- **Pre-allocate Collections:** When copying or constructing collections (like arrays, lists, or dictionaries) of a known or predictable size, always initialize them with their capacity pre-allocated (e.g., `new List<GameMod>(estimatedCount)`) to avoid cost-heavy internal array resizing operations.

## 19. Structured Logging
- **Avoid String Interpolation in Logs:** Do not use string interpolation (`$"..."`) inside logging statements (e.g., avoid `_logger.LogInformation($"Parsed {count} mods")`). This allocates memory even if that log level is currently disabled. Instead, use structured/semantic logging templates:
  ```csharp
  _logger.LogInformation("Parsed {ModCount} mods for player {PlayerId}", count, playerId);
  ```

## 20. Cooperative Cancellation
- **Propagate Cancellation Tokens:** For all long-running, CPU-bound, or I/O-bound asynchronous methods (e.g., database execution, network calls, scraper loops), accept a `CancellationToken` parameter (defaulted to `default`) and propagate it down to nested asynchronous calls:
  ```csharp
  public async Task FetchDataAsync(string playerId, CancellationToken cancellationToken = default)
  ```
- **Responsive UI Cancellation:** Wire background processes to UI cancellation states (like a "Cancel" button or window closing action) to cleanly interrupt active tasks and prevent orphaned threads.

## 21. Dependency Injection Lifetime Management
- **Avoid Captive Dependencies:** Never inject a service with a shorter lifetime into a service with a longer lifetime (e.g., injecting a transient `DbContext` or short-lived unit-of-work directly into a singleton-level service).
- **Lifetime Selection:**
  - **Singleton:** Use for stateless utility services, network coordinators (like `ComlinkService`), and persistent caches.
  - **Transient:** Use for ViewModels, Views, and specialized calculation components that are created and disposed of frequently.
  - **Scoped Processing:** Since desktop applications run in a single continuous process without HTTP request boundaries, use an `IServiceScopeFactory` to construct a temporary unit-of-work scope for distinct background transactions (such as running a database sync sequence).

## 22. High-Performance JSON Serialization
- **Prefer System.Text.Json:** Avoid importing external JSON frameworks like `Newtonsoft.Json`. Use the native high-performance `System.Text.Json` namespace.
- **Re-use JsonSerializerOptions:** Never instantiate `new JsonSerializerOptions()` inside hot paths or repeat method calls. Store options in a `static readonly` or `const` field (or register them via DI) to leverage internal metadata caching and avoid massive GC pressure.
- **Source Generators:** Consider utilizing `JsonSerializerContext` (JSON Source Generation) for high-performance, ahead-of-time (AOT) compiled serialization of game data payloads if startup speed or memory footprints become constrained.

## 23. Test Isolation & Mocking Boundaries
- **Mocking External Boundaries:** Never allow unit tests to hit live databases or external network services. Mock external network/API targets using interfaces (e.g., `IComlinkService`, `IHttpClientFactory`) or specialized test servers.
- **Strict Mocking Rules:** Avoid using concrete implementations for dependencies in unit tests unless they are stateless, pure data carriers, or mathematical utility classes.
- **In-Memory SQLite for Integration Testing:** For repository integration tests, use SQLite in-memory mode (`Data Source=:memory:`) rather than mocking the `DbContext` directly, ensuring exact query logic and constraint validations are verified.

## 24. Commit & Git Guidelines
- **Conventional Commits:** Adhere to the Conventional Commits specification for all repository commit messages to keep the changelog automated and structured:
  - `feat: ...` for new user-facing features.
  - `fix: ...` for bug fixes.
  - `refactor: ...` for code changes that neither fix a bug nor add a feature.
  - `test: ...` for adding or correcting tests.
  - `docs: ...` for documentation updates.
- **Small, Atomic Pull Requests:** Keep pull requests focused on a single logical task (e.g., implementing one service or fixing one UI page) to make code reviews easy, clean, and context-conscious for both humans and AI models.

## 25. Native AOT & Trimming Compatibility
- **Avoid Dynamic Reflection:** To support publishing the application via NativeAOT (for instant startup times) and Assembly Trimming, avoid runtime dynamic reflection (such as untyped `Assembly.Load`, `Type.GetType()`, or `dynamic` keyword variables).
- **AOT Annotations:** If reflection or dependency scanning is absolutely necessary, explicitly decorate classes/methods with trim-friendly analysis attributes (e.g., `[RequiresUnreferencedCode]` or `[DynamicallyAccessedMembers]`).

## 26. Atomic Configuration & Settings Persistence
- **No Registry Writes:** Never use the Windows Registry or OS-specific proprietary config engines for storage. Store all user configurations (e.g., current Player ID, theme options, custom priority profiles) in a localized, schema-validated JSON format under the user's local application data folder.
- **Atomic Writing:** To prevent settings corruption in the event of an abrupt power-loss or application crash during save-operations, always use an atomic write strategy:
  1. Serialize configuration output to a temporary staging file (`settings.json.tmp`).
  2. Verify the serialization completed without throwing an exception.
  3. Atomically overwrite the active settings file using `File.Replace` or `File.Move(..., overwrite: true)`.

## 27. Global Unhandled Exception Safeguards
- **Global Error Handlers:** Always register global exception handlers during application startup (e.g., in `Program.cs` or `App.axaml.cs`). Bind handlers to `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` to log fatal crashes before the application terminates.
- **Avoid `async void`:** Never use `async void` except for event handlers or parameterless commands where it is syntactically required. Exceptions thrown in `async void` methods cannot be caught by standard call-stack wrappers and will abruptly crash the entire application process.

## 28. UI Localization & Internationalization (I18n)
- **No Hardcoded UI Strings:** Do not hardcode user-facing labels, button texts, tooltips, or error notifications in `.axaml` markup files or ViewModel properties. Use localizable resource assets (such as `.resx` files or localized resource dictionaries) to host all user-facing strings.
- **Dynamic Language Switching:** When binding localized strings in Avalonia, prefer dynamic markup extensions or binding strategies that can respond to runtime culture switches without requiring an application restart.
