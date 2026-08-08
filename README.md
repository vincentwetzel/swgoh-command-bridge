# SWGOH Command Bridge

A cross-platform desktop application written in C# and Avalonia UI for read-only SWGOH roster, mod inventory, and mod optimization analysis.

The application is an analysis tool: it reads account data through a local `swgoh-comlink` proxy, stores a private local cache, and produces upgrade, assignment, and roster-coverage recommendations. It does not equip, upgrade, slice, or sell mods, and it never writes to the game account.

## Current Shape
- `src/swgoh-command-bridge.Core` contains the compiled domain models, EF Core/SQLite persistence, repositories, Comlink/settings/filter/mechanics services, mod advisor/assignment services, and the `swgoh.gg` scraper.
- `src/swgoh-command-bridge.UI` contains the Avalonia shell, navigation, diagnostics, and feature viewmodels for characters, mods, priorities, thresholds, and optimization.
- Player sync now maps live `swgoh-comlink` payloads into cached player, character, and mod entities, preserves character priorities, supports cached-account switching, and reports tolerant-parser warnings and phase progress. Completed, failed, cancelled, and interrupted sync attempts are retained as bounded privacy-safe history. Scraped `swgoh.gg` recommendations are cached locally with stale-data protection, cancellation, bounded retries, rate-limit handling, and per-character failure summaries.
- The optimizer supports deterministic priority-first planning and a bounded joint roster plan that reserves each mod once. It reports conflicts, alternatives, swap candidates, and persisted mod-stat projections as analysis estimates rather than guaranteed in-game gains.
- `tests/swgoh-command-bridge.Tests` contains focused xUnit coverage for operation states, settings and transfer validation, cache/repository behavior, player parsing and sync, diagnostics, view-model workflows, mod filtering/mechanics/advisor decisions, assignment planning, and recommendation parsing/scraping.

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

On first launch, the app creates a SQLite cache and JSON settings under the case-stable `SWGOHCommandBridge` directory inside the platform's local application-data directory. Cache, settings, diagnostics, and backups share that directory. The exact paths are shown on the Settings and Diagnostics screens. Configure the Comlink URL, ally code, and Dark/Light/System theme in Settings, then use Home to sync. Home can switch to any cached account without contacting Comlink; syncing, recommendation scraping, cache backup/restore/reset, and account removal are explicit user actions.

The application writes only local cache, settings, backup, and diagnostics files. Diagnostics and exported settings are redacted or credential-safe by design; review any exported report before sharing it. See [DIAGNOSTICS.md](docs/DIAGNOSTICS.md) and [COMLINK_SETUP.md](docs/COMLINK_SETUP.md) for the privacy and recovery boundaries.

## Documentation
- [Project specification](docs/SPEC.md)
- [Architecture overview](docs/ARCHITECTURE.md)
- [Comlink setup](docs/COMLINK_SETUP.md)
- [Diagnostics and support](docs/DIAGNOSTICS.md)
- [Mod mechanics](docs/MOD_MECHANICS.md)
- [State flow](docs/STATE_FLOW.md)
- [Smoke-test checklist](docs/SMOKE_TEST_CHECKLIST.md)
- [Release guide](docs/RELEASE_GUIDE.md)
- [File manifest](docs/FILE_MANIFEST.md)
- [Completed implementation audit](docs/ROADMAP_HISTORY.md)
- [Changelog](CHANGELOG.md)
- [Coding standards](CODING_STANDARDS.md)
- [Roadmap](TODO.md)

The roadmap is split intentionally: `ROADMAP_HISTORY.md` records completed implementation work, while `TODO.md` contains only release gates and deferred product scope. The current release target is a read-only desktop build; packaged runtime and visual smoke verification remain release-operator gates.
