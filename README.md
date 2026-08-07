# SWGOH Command Bridge

A cross-platform desktop application written in C# and Avalonia UI for read-only SWGOH roster, mod inventory, and mod optimization analysis.

The application is an analysis tool: it reads account data through a local `swgoh-comlink` proxy, stores a private local cache, and produces upgrade, assignment, and roster-coverage recommendations. It does not equip, upgrade, slice, or sell mods.

## Current Shape
- `src/swgoh-command-bridge.Core` contains the compiled domain models, EF Core/SQLite persistence, repositories, Comlink/settings/filter/mechanics services, mod advisor/assignment services, and the `swgoh.gg` scraper.
- `src/swgoh-command-bridge.UI` contains the Avalonia shell, navigation, diagnostics, and feature viewmodels for characters, mods, priorities, thresholds, and optimization.
- Player sync now maps live `swgoh-comlink` payloads into cached player, character, and mod entities. Scraped `swgoh.gg` recommendations are cached locally with stale-data protection and progress reporting.
- `tests/swgoh-command-bridge.Tests` contains focused xUnit coverage for operation state handling, settings persistence, player sync/mapping, mod filtering, and mod advisor decisions.

The root-level C# files are historical drafts. The solution compiles the projects under `src/`; see [FILE_MANIFEST.md](docs/FILE_MANIFEST.md) for the active layout.

## Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) to run the local `swgoh-comlink` proxy

## Getting Started
1. Clone this repository.
2. Run the local comlink proxy by following [COMLINK_SETUP.md](docs/COMLINK_SETUP.md).
3. Restore and build the solution:

   ```bash
   dotnet restore
   dotnet build swgoh-command-bridge.sln
   ```

4. Run the Avalonia UI project:

   ```bash
   dotnet run --project src/swgoh-command-bridge.UI/swgoh-command-bridge.UI.csproj
   ```

5. Run the test suite:

   ```bash
   dotnet test swgoh-command-bridge.sln
   ```

On first launch, the app creates a SQLite cache and JSON settings under the platform's local application-data directory. The exact paths are shown on the Settings and Diagnostics screens. Configure the Comlink URL and ally code in Settings, then use Home to sync.

## Documentation
- [Project specification](docs/SPEC.md)
- [Architecture overview](docs/ARCHITECTURE.md)
- [Comlink setup](docs/COMLINK_SETUP.md)
- [Diagnostics and support](docs/DIAGNOSTICS.md)
- [Mod mechanics](docs/MOD_MECHANICS.md)
- [State flow](docs/STATE_FLOW.md)
- [Smoke-test checklist](docs/SMOKE_TEST_CHECKLIST.md)
- [File manifest](docs/FILE_MANIFEST.md)
- [Coding standards](CODING_STANDARDS.md)
- [Roadmap](TODO.md)
