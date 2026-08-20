# SWGOH Command Bridge

A desktop application written in C# and Avalonia UI for read-only SWGOH roster, mod inventory, and mod optimization analysis. The UI and Core target .NET 8 across desktop platforms; Windows x64 is the primary release target because it is currently the only platform with automatic Comlink installation.

The application is an analysis tool: it reads account data through a configured `swgoh-comlink` proxy, stores a private local cache, and produces upgrade, assignment, and roster-coverage recommendations. It does not equip, upgrade, slice, or sell mods, and it never writes to the game account.

## Current Shape
- `src/swgoh-command-bridge.Core` contains the compiled domain models, EF Core/SQLite persistence, repositories, Comlink/settings/filter/mechanics services, mod advisor/assignment services, the managed Comlink runtime coordinator, and the `swgoh.gg` scraper.
- `src/swgoh-command-bridge.UI` contains the Avalonia shell, navigation, diagnostics, and feature viewmodels. Its primary workspaces are Roster (character inspection and character/ship priorities), Mods (inventory and upgrade rules), and Optimize (character and roster assignment planning). Dashboard, Settings, and Diagnostics support account management and application operations. The priority screen is a drag-and-drop S/A/B/C/D tier board with an Unranked holding area and separate character/ship views.
- Character presentation uses the authoritative catalog for localized names, bundled portraits, alignment-aware gear/relic frame highlights, and initials fallbacks when an asset is unavailable. Catalog alignment and relic tiers are persisted with the account cache so portraits remain consistent across reloads and syncs.
- Player sync now maps live `swgoh-comlink` payloads into cached player, character, and mod entities, preserves priority-board placement across syncs, supports cached-account switching, and reports tolerant-parser warnings and phase progress. Completed, failed, cancelled, and interrupted sync attempts are retained as bounded privacy-safe history. Scraped `swgoh.gg` recommendations are cached locally with stale-data protection, cancellation, bounded retries, rate-limit handling, and per-character failure summaries.
- Mod primaries are normalized against the canonical shape rules during parsing and cache mapping. Impossible source pairs are corrected only where the legacy identifier is unambiguous; other invalid pairs become unavailable and are excluded from guidance.
- The optimizer supports deterministic priority-first planning and a bounded joint roster plan that reserves each mod once. It reports conflicts, alternatives, swap candidates, and persisted mod-stat projections as analysis estimates rather than guaranteed in-game gains.
- Preferred mod guidance is a global, aggregated snapshot of high-ranking GAC accounts. It is bundled with the app, cached offline, refreshed silently from the repository when available, and distinguishes a preferred primary from viable alternatives when top-player usage is split.
- `tests/swgoh-command-bridge.Tests` contains focused xUnit coverage for operation states, settings and transfer validation, cache/repository behavior, player parsing and sync, diagnostics, view-model workflows, mod filtering/mechanics/advisor decisions, assignment planning, and recommendation parsing/scraping.

The solution compiles the projects under `src/`; see [FILE_MANIFEST.md](docs/FILE_MANIFEST.md) for the active layout and bundled asset boundaries.

## Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Windows x64 for automatic Comlink setup (internet is required only when the pinned runtime is not already cached), or an externally managed Comlink endpoint on another platform

## Getting Started
1. Clone this repository.
2. Restore and build the solution:

   ```bash
   dotnet restore
   dotnet build swgoh-command-bridge.sln
   ```

3. Run the Avalonia UI project:

   ```bash
   dotnet run --project src/swgoh-command-bridge.UI/swgoh-command-bridge.UI.csproj
   ```

4. Run the test suite:

   ```bash
   dotnet test swgoh-command-bridge.sln
   ```

At startup, the app creates a SQLite cache and JSON settings under the case-stable `SWGOHCommandBridge` directory inside the platform's local application-data directory. On Windows x64, it checks the configured local endpoint and, when necessary, installs and starts a pinned managed Comlink runtime there. A healthy existing local service or any non-local endpoint is used as configured; on Linux and macOS, run Comlink separately. Cache, settings, diagnostics, backups, the verified character catalog, the preferred-mod dataset, and the managed Windows runtime share that directory. The app starts with embedded catalog and preferred-mod baselines, then performs best-effort refreshes without blocking cached data; a failed refresh leaves the last verified version in place. Configure the Comlink URL, default ally code, theme, optional recommendation contact email, and local scraping policy in Settings. Use the account switcher in the top-right shell area to add an account, sync it, switch among cached accounts offline, refresh the active account, or remove a cached account. On startup, a stale active cache may refresh once in the background after cached data is available; the previous cache remains usable if that refresh fails. Recommendation scraping, cache backup/restore/reset, and account removal remain explicit user actions.

The application writes only local cache, settings, backup, and diagnostics files. Diagnostics and exported settings are redacted or credential-safe by design; review any exported report before sharing it. See [DIAGNOSTICS.md](docs/DIAGNOSTICS.md) and [COMLINK_SETUP.md](docs/COMLINK_SETUP.md) for the privacy and recovery boundaries.

## Documentation
- [Project specification](docs/SPEC.md)
- [Architecture overview](docs/ARCHITECTURE.md)
- [Comlink setup](docs/COMLINK_SETUP.md)
- [Preferred GAC mod-data design](docs/PREFERRED_MOD_DATA_DESIGN.md)
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
- [Agent and background-process boundaries](docs/AGENTS.md)
- [Preferred-mod publisher guide](tools/swgoh-command-bridge.PreferredModsPublisher/README.md)

The roadmap is split intentionally: `ROADMAP_HISTORY.md` records completed implementation work, while `TODO.md` contains only release gates and deferred product scope. The current release target is a read-only desktop build; packaged runtime and visual smoke verification remain release-operator gates.
