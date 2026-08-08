# TODO Roadmap - Release Readiness

Audit date: 2026-08-07

## Current assessment

The current vertical slice is implemented but is not yet release-ready:

- `MainWindow` now routes through the primary data screens and Home exposes account sync.
- Startup now creates or repairs the required local tables, runs a transactional versioned SQLite compatibility migrator, records applied migrations, and exposes a retryable shell error when cache initialization fails; full EF migrations and deeper recovery are still missing.
- Settings, Comlink, repositories, player sync, advisor, assignment, diagnostics, and database-backed view models are now composed directly by the shell; this still needs a proper host/lifetime design.
- Runtime sample inventory/loadout fallbacks have been removed; remaining work is primarily hardening, representative fixture coverage, packaging, and release verification.
- Build, test, and packaged-runtime verification are intentionally left to the release operator; the smoke-test procedure is documented in `docs/SMOKE_TEST_CHECKLIST.md`.

The screenshots explained the original state of the source. The next milestone is to validate and harden this vertical slice, then finish the account and optimizer workflows.

### Current implementation progress

The working tree contains a vertical-slice implementation: first-launch schema creation with a narrow SQLite compatibility upgrade, real shell navigation, Home account-sync controls, database-backed Characters/Priorities/Mods/Optimizer/Threshold screens, persisted threshold editing, removal of runtime sample inventory/loadout data, persisted mod stat snapshots, a unified scraper/optimizer recommendation payload, privacy-redacted diagnostics, and portable cache backup/reset controls.

## Audit status of previous claims

Legend: **Verified** means present in the active `src/` projects and supported by focused tests or direct source inspection. **Partial** (`[~]`) means code exists but is not wired, incomplete, or contradicted by another layer. **Open** (`[ ]`) means the feature is not usable from the application.

### Verified foundation

- [x] **Core project structure** — Core, UI, and test projects are in the solution; EF Core/SQLite, Avalonia, and xUnit scaffolding exist.
- [x] **Domain models and core rule services** — Character/mod models, operation states, filtering, mechanics, advisor, assignment, settings, and scraper service types exist.
- [x] **Basic isolated test coverage** — Player JSON parsing, settings persistence, operation states, mod filtering, advisor decisions, and scraper parsing tests exist.
- [x] **Local persistence types** — Player, character, mod, and `swgoh.gg` recommendation entities plus repository methods exist.

### Reopened or downgraded claims

- [~] **Comlink integration** — Settings now edits and tests the configured URL, Comlink sends explicit JSON/client headers, retries transient failures, and UI failures are categorized without exposing exception/URL text; deployment-specific authentication remains external to persisted settings and richer diagnostics remain.
- [x] **Complete player mapping** - `PlayerProfileParser` accepts bounded `data`/`player`/`profile`/`payload` envelopes plus common roster/inventory aliases, mixed numeric/string values, direct or nested stat shapes, nested display-name metadata, and optional Comlink metadata catalogs; it preserves stars, suppresses duplicate mod records, isolates malformed equipped/inventory entries, and returns record-level diagnostics. Representative nested-envelope and complete inventory fixtures cover the supported contract.
- [~] **Account sync and UI refresh** — The Home command validates nine-digit ally codes, invokes `PlayerService.SyncPlayerProfileAsync`, shows connecting/mapping/persisting/completed phases, refreshes Characters, Priorities, and Mods, reports active character/mod cache counts with actionable Home navigation, exposes cancellation/retry with parser warnings, switches among cached ally-code-scoped accounts offline, removes selected cached accounts with confirmation, and persists bounded completed/failed/cancelled sync outcomes with counts and warning totals; Home and Diagnostics now show the latest result and Diagnostics shows the recent redacted attempt history, while richer filtering remains open.
- [~] **Basic UI** - Characters, Priorities, Thresholds, Mods, Optimizer, Settings, and Diagnostics are reachable and database-backed with first-run empty states; richer inventory presentation and visual-state coverage remain open.
- [~] **Navigation** — Home, Characters, Priorities, Mods, Optimizer, Thresholds, Settings, and Diagnostics are reachable, and shell command coverage now verifies each route; broader recovery and visual-state coverage remain.
- [x] **Explicit UI states** — Main data screens now expose visible loading, empty, success, and error states with retry actions where applicable; runtime sample data was removed, shared transitions are covered, and Diagnostics now has explicit unavailable-cache retry coverage.
- [ ] **Cross-platform runnable product** — Platform support and packaging have not been demonstrated in this audit.
- [x] **Focused test cleanup** - Comlink payloads now live in named reusable fixtures, and PlayerService mapping tests separate payload data from test helpers.
- [x] **Character priorities** - The screen loads cached characters, validates the 0-100 range, preserves selection after refresh, and provides tested dirty-state cancel behavior plus explicit empty/error states.
- [x] **Threshold management** - Thresholds have stable persisted IDs, duplicate/save/delete flows, explicit finite-value validation, a selectable default, versioned JSON import/export, transactional in-memory rollback on persistence failure, and lifecycle/empty-state view-model coverage; advisor efficiency semantics and settings migration are covered.
- [~] **Upgrade/swap/sell advisor** — The Mods screen now analyzes selected mods against cached prioritized characters and equipped mods, with deterministic action ordering, hard rarity floors, level/slice potential, 5-dot slicing, compatible swap checks, and score/reason details; richer stat semantics and projected-value rules remain.
- [~] **Detailed mod mechanics in the product** - Primary and secondary stat snapshots now survive sync and are used by the active advisor path; selected-mod details now render readable secondary-stat lines and set/slot labels, assignment scoring includes persisted secondary-stat quality with explicit score deltas, and the optimizer projects persisted mod-stat deltas against current equipped mods while clearly excluding base stats/set bonuses/conversion rules; full UI explanations and real-data rule coverage remain.
- [~] **Advanced inventory UI** - The screen now loads SQLite data and exposes search, slot/set/primary/structured secondary-stat combinations, ownership, pips, minimum-level, tier, sorting, owner labels, selected-mod details, filter result counts, and a one-click filter reset; virtualization and richer stat presentation remain.
- [~] **`swgoh.gg` recommendations** — The optimizer can refresh selected or all-character recommendations with progress, cancellation, privacy-safe failure reasons, configurable HTTP contact metadata, bounded response bodies, retries, rate-limit handling, stale checks, and provenance; broader real-page fixture coverage and release/legal policy remain.
- [~] **Recommendation assignment** — The active single-character service now returns a `ModLoadoutResult` with completeness, set-rule validity, deterministic selection, per-mod reasons, lower-ranked alternatives, and equipped-mod swap candidates while scoring actual persisted stats and community recommendations; roster planning now reports missing, reserved, and set-constraint slots, consolidates actionable swaps with availability/reservation context, and exposes a bounded joint optimizer, while a full-scale solver remains.
- [~] **Recommendation UI** — `ModOptimizerView` is now reachable, shows real cached loadouts or explicit empty/error states, identifies current/stale/missing recommendation data, exposes provenance, renders loadout status plus per-mod explanations, combined score context, projected mod-stat impact, and can calculate either a priority-first roster plan or a bounded global plan with conflict summaries and consolidated swap candidates; richer game-stat simulation remains.

## Milestone 0: Make the application actually runnable

*Exit criterion: a fresh install opens a useful first-run screen, creates its local schema, and every visible control either works or clearly explains its unavailable state.*

- [~] Add a real application composition root/host for `AppDbContext`, settings, logging, `HttpClient`/Comlink, repositories, player services, scraper, advisor, and assignment services; `ApplicationComposition` now owns the default graph, supports injected settings for isolated shell tests, and disposes long-lived resources on window close, while broader host configuration remains.
- [~] Initialize the database on startup using an explicitly temporary `EnsureCreated` plus a transactional versioned SQLite compatibility migrator that repairs missing required tables/columns and reports readable retryable startup errors; full versioned EF migrations and deeper recovery remain.
- [~] Define database/settings paths consistently and document backup, reset, migration, and settings transfer behavior; cache, settings, diagnostics, and backup paths now share `AppDataPaths`, while timestamped SQLite backup, guarded reset, schema-version markers, idempotent cache migration results, persisted player sync timestamps, versioned persisted settings, and versioned settings JSON transfer are implemented. Full versioned EF migrations remain.
- [x] Replace the current `MainWindowViewModel` demo data and legacy assignment dependency with database-backed feature view models.
- [~] Build real navigation for Home, Characters, Priorities, Mods, Optimizer, Thresholds, Settings, and Diagnostics; Home owns the account-sync controls and provides direct actions into the active cached account's Characters, Mods, and Optimizer screens.
- [x] Add a shared loading/empty/success/error state pattern to every data screen, including retry actions and user-visible error messages; `StateViewModelBase<T>` now centralizes the common projections while each screen retains its specialized state flags, and shared transition coverage is present.
- [x] Remove all fabricated runtime data and make empty first-run states intentional onboarding states.
- [~] Add a small smoke-test checklist for fresh database, empty database, populated cache, Comlink unavailable, and malformed cache cases; the checklist is documented, but execution remains outstanding.

## Milestone 1: First-run account setup and reliable cache

*Exit criterion: a user can configure Comlink, enter an ally code, sync a roster, close/reopen the app, and view the cached data offline.*

- [x] Add Settings UI for Comlink URL, default ally code, theme, reset/cache actions, and versioned settings import/export; theme choices are normalized and applied at startup, save, and import, while richer profile management remains future scope.
- [x] Validate and apply the configured Comlink URL through the actual `HttpClient` base address.
- [x] Add sync command, cancellation, bounded transient retry, and a concise result summary; phase progress, parser-warning counts, cancellation preservation, categorized failures, retry availability, and final Home summaries are now covered.
- [x] Confirm the supported Comlink response contract and support the complete roster plus unequipped inventory mod payload; bounded envelope unwrapping, tolerant aliases, nested metadata names, optional metadata-catalog enrichment, partial-record handling, equipped/inventory loss diagnostics, ally-code validation, and representative nested-envelope fixtures now cover the known shapes.
- [x] Persist all mod data needed by analysis: primary stat, secondary stats, roll counts, equipped owner, level, tier, pips, slot, and set; sync mapping and persistence coverage now asserts the complete snapshot.
- [x] Preserve user-owned fields such as character priorities when refreshing server-owned roster data.
- [x] Scope character, priority, mod, optimizer, recommendation, and incremental scraper refreshes to the active ally code; Home switches among cached accounts offline, removes a selected cached account after confirmation, and recommendation cache keys are composite character/ally-code keys with a legacy migration.
- [x] Make repository replacement/upsert and cached-account removal transactional and safe for repeated syncs; replacement rollback coverage now proves existing account rows survive a failed refresh, while migration integration remains a separate database milestone.
- [x] Add integration tests using representative Comlink fixtures, including empty roster, inventory mods, malformed records, duplicate records, and partial responses.

## Milestone 2: Usable character and mod inventory screens

*Exit criterion: synced data is visible and useful without opening a debugger or editing files.*

- [x] Implement the Characters screen with search, roster metadata, priority display, and real empty/loading/error states.
- [x] Implement the Priorities screen with editable range validation, save/cancel behavior, dirty-state handling, refresh after save, and focused lifecycle/state tests.
- [x] Implement the Mods screen against SQLite rather than sample objects.
- [x] Expose filters for slot, set, primary, secondary combinations, equipped status, level, pips, tier, sorting, and text search; comma-separated AND criteria, numeric comparisons, result counts, active-account scoping, and one-click filter reset are covered by the inventory filter matrix.
- [x] Add stable sorting, pagination/virtualization, selected-mod detail rendering, and clear equipped-owner labels; the Mods screen uses deterministic tie-breakers, 100-item paging, an explicit `VirtualizingStackPanel`, readable selected details, roster-derived owner names, and tested stale-recommendation protection.
- [x] Convert persisted entities to domain models without dropping stats; `PersistedModelMapper` is a Core boundary used by the Mods screen and directly tests character fields, mod stats, roll counts, ownership, and malformed-stat tolerance.
- [~] Make advisor thresholds selectable and load them from persisted settings; the Mods screen now shows the active saved threshold and refreshes recommendations when it changes, while broader rule coverage remains.

## Milestone 3: Real threshold and advisor workflow

*Exit criterion: a user can define rules, evaluate real inventory mods, and understand every recommendation.*

- [x] Design the threshold storage format and migrate it from the current settings placeholder if needed; persisted settings now use a versioned envelope and legacy threshold records receive stable IDs and a valid default during load/import.
- [x] Implement create, edit, duplicate, delete, select-default, validation, and versioned JSON import/export for thresholds; the full UI lifecycle, non-finite numeric rejection, persistence rollback, legacy-array migration, and unsupported-version coverage are tested.
- [x] Define exact upgrade/slice/sell/swap semantics, including missing stats, 5-dot slicing, set/primary compatibility, equipped ownership, ties, and threshold efficiency; the active advisor now reports deterministic current and projected secondary-roll efficiency estimates and applies them to upgrade, slice, keep, swap, and sell decisions.
- [~] Supply the advisor with the active ally code's cached prioritized roster and real equipped-mod context; cached-account switching/removal now exists, while richer roster context remains.
- [~] Separate recommendation calculation from UI and add table-driven tests for every decision branch; calculation already lives in Core and the decision matrix now covers rarity, level, tier, slice, keep, sell, and swap branches, while projected-value semantics remain.
- [~] Add explanations that identify the action, score, current values, and affected character/mod; assignment-score deltas and projected persisted mod-stat deltas now appear for alternatives, swaps, and loadouts with explicit warnings about game-stat limitations, while projected-value and rule identifiers remain.

## Milestone 4: Recommendation data and optimizer

*Exit criterion: recommendations can be refreshed safely and produce deterministic, explainable, valid loadouts from the user’s actual inventory.*

- [x] Define one canonical recommendation schema shared by scraper, database, assignment service, and UI; `RecommendationSnapshot` now preserves character/account scope alongside source, payload version, scrape time, set percentages, and per-slot primary percentages across every boundary.
- [x] Replace brittle regex-only parsing with fixture-backed parsing that tolerates current page variations, missing sections, localization, and changed markup; section boundaries now prevent adjacent recommendations from cross-associating, and reusable fixtures cover flexible attributes, quote styles, slot markers, localized stat text, nested markup, and absent sections.
- [~] Add request policy, user-agent/contact information, rate-limit handling, cancellation, stale-data policy, and per-character failure reporting; bounded retries, `Retry-After` handling, cancellation propagation, stale checks, privacy-safe endpoint failure reasons, configurable `From` contact metadata, and a 2 MiB response-size guard are implemented, while broader policy/legal review remains.
- [x] Add a user-triggered scrape/update workflow with selected-character and all-character incremental refresh, progress, cancellation, status, and an explicit validated retry/backoff/pacing policy; the last-run summary is persisted and transient responses are covered by deterministic tests.
- [~] Decide whether local scraping is acceptable for release; local scraping is now an explicit persisted Settings policy and can be disabled while cached recommendations remain readable, but the final release/legal decision and central recommendation service remain open.
- [x] Implement deterministic six-slot assignment with no duplicate mod reuse, popularity-weighted set and primary matching, set-bonus rules, lower-ranked slot alternatives, equipped-mod swap candidates, and explicit "not enough inventory" results; the single-character contract reports incomplete inventory and invalid set distributions with deterministic score explanations.
- [~] Implement roster-wide coverage planning and swap recommendations as a single service contract; the assignment service now supports deterministic priority-first planning plus bounded joint optimization, reserves mods once per result, reports missing/reserved/set-constraint conflicts, and returns consolidated swap candidates with roster reservation context, while a full-scale global solver remains.
- [~] Show recommendation provenance, match reasons, alternatives, expected benefit, and stale/missing-data states in the optimizer; current/stale/missing status, provenance, per-mod explanations, lower-ranked alternatives, swap candidates, projected persisted mod-stat impact, and priority/global roster modes are visible, while richer game-stat simulation remains.
- [~] Add end-to-end optimizer tests with competing mods, equipped mods, missing slots, duplicate candidates, set constraints, and community-data alternatives; persisted recommendation provenance and assignment explanations now have an integration case, while the remaining conflict matrix is open.

## Milestone 5: Product hardening and release readiness

*Exit criterion: another user can install, configure, use, recover from common failures, and understand the product’s limits.*

- [x] Rename `UnitTest1.cs` and split fixtures from helpers; named Comlink payload fixtures now live under `tests/.../Fixtures` and no stale root-level duplicate remains.
- [~] Add UI/view-model tests for navigation, state transitions, commands, validation, and no-data behavior; Settings, Characters, Priorities, Mods, Optimizer, shell navigation, and unavailable-cache error/retry paths now have focused coverage, while broader fault injection and visual-state coverage remain.
- [~] Add database migration tests and a disposable test database strategy that never touches the user cache; transactional schema-version coverage now includes partial legacy tables and unsupported future backups, settings tests use isolated temporary directories, while a complete EF migration suite remains.
- [x] Add structured diagnostics/log export with privacy-safe redaction of ally codes and account payloads; Diagnostics now includes bounded UI and Core logger event capture for startup, sync, cache, settings, and scraper support events.
- [~] Define supported OS/runtime matrix, publish profiles, self-contained versus framework-dependent packaging, and upgrade behavior; the release guide defines Windows primary and Linux/macOS candidate targets, publish commands, trimming policy, recovery expectations, and release gates, while artifact verification remains outstanding.
- [~] Add crash-safe cache recovery, backup/reset tooling, and clear offline behavior; startup retry, verified backup/restore, guarded reset, explicit cached-account selection, empty-scope isolation, and confirmed account removal now exist.
- [x] Reconcile README, architecture, state-flow, Comlink setup, file manifest, and coding standards with the shipped behavior.
- [x] Document the exact definition of "read-only" in the README, specification, and smoke-test checklist.

## Future scope after the read-only product is stable

- [ ] Central recommendation server and shared cache.
- [ ] Multiple saved loadouts per character.
- [ ] Grand Arena multi-squad planning.
- [ ] Dashboards and data visualizations.
- [ ] Theme customization beyond the initial theme setting.
- [ ] Any write-access feature only after a separate security/authentication design, threat model, consent flow, audit trail, and rollback plan.
