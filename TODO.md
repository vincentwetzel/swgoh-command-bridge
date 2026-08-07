# TODO Roadmap - Release Readiness

Audit date: 2026-08-07

## Current assessment

The current vertical slice is implemented but is not yet release-ready:

- `MainWindow` now routes through the primary data screens and Home exposes account sync.
- Startup now creates the local schema, runs a transactional versioned SQLite compatibility migrator, records applied migrations, and exposes a retryable shell error when cache initialization fails; full EF migrations and deeper recovery are still missing.
- Settings, Comlink, repositories, player sync, advisor, assignment, and database-backed view models are now composed directly by the shell; this still needs a proper host/lifetime design.
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

- [~] **Comlink integration** — Settings now edits and tests the configured URL, and Comlink retries transient failures; authentication and richer diagnostics remain.
- [~] **Complete player mapping** — `PlayerProfileParser` now accepts common roster/inventory aliases, mixed numeric/string values, direct or nested stat shapes, preserves stars, suppresses duplicate mod records, and isolates malformed entries. Broader real Comlink fixtures and reliable display-name metadata mapping remain.
- [~] **Account sync and UI refresh** — The Home command invokes `PlayerService.SyncPlayerProfileAsync`, refreshes Characters, Priorities, and Mods, and exposes cancellation/retry with a concise count summary; progress and richer result reporting remain open.
- [~] **Basic UI** — Characters, Priorities, Thresholds, Mods, and Optimizer are now reachable and database-backed with first-run empty states; optimizer scrape actions and full inventory filters remain open.
- [~] **Navigation** — Home, Characters, Priorities, Mods, Optimizer, Thresholds, Settings, and Diagnostics are reachable; deeper support workflows remain.
- [~] **Explicit UI states** — Main data screens now expose loading, empty, success, and error states, and runtime sample data was removed. State coverage and retry behavior still need broader testing.
- [ ] **Cross-platform runnable product** — Platform support and packaging have not been demonstrated in this audit.
- [~] **Focused test cleanup** — The scraper fixture now has a descriptive filename; fixture/helper separation and end-to-end coverage remain.
- [~] **Character priorities** — The screen now loads cached characters, validates the 0–100 range, preserves selection after refresh, and provides dirty-state cancel behavior; focused UI tests remain.
- [~] **Threshold management** — Thresholds now have stable persisted IDs, duplicate/save/delete flows, explicit validation, a selectable default, and versioned JSON import/export; richer rule semantics, settings-wide migration, and UI test coverage remain.
- [~] **Upgrade/swap/sell advisor** — The Mods screen now analyzes selected mods against cached prioritized characters and equipped mods, with deterministic action ordering, hard rarity floors, level/slice potential, 5-dot slicing, compatible swap checks, and score/reason details; richer stat semantics and projected-value rules remain.
- [~] **Detailed mod mechanics in the product** — Primary and secondary stat snapshots now survive sync and are used by the active advisor path; full UI explanations and real-data rule coverage remain.
- [~] **Advanced inventory UI** — The screen now loads SQLite data and exposes search, slot/set/primary/structured secondary-stat combinations, ownership, pips, minimum-level, tier, sorting, owner labels, and selected-mod details; virtualization and richer stat presentation remain.
- [~] **`swgoh.gg` recommendations** — The optimizer can refresh selected or all-character recommendations with progress and cancellation; parser/network policy, rate-limit handling, and provenance still need hardening.
- [~] **Recommendation assignment** — The active single-character service now returns a `ModLoadoutResult` with completeness, set-rule validity, deterministic selection, and per-mod reasons while scoring actual persisted stats and community recommendations; roster coverage and swap output still need consolidation with the legacy planner.
- [~] **Recommendation UI** — `ModOptimizerView` is now reachable, shows real cached loadouts or explicit empty/error states, identifies current/stale/missing recommendation data, exposes provenance, renders loadout status plus per-mod explanations, and can calculate a priority-first roster plan with conflict summaries; alternatives and expected-benefit comparisons remain.

## Milestone 0: Make the application actually runnable

*Exit criterion: a fresh install opens a useful first-run screen, creates its local schema, and every visible control either works or clearly explains its unavailable state.*

- [~] Add a real application composition root/host for `AppDbContext`, settings, logging, `HttpClient`/Comlink, repositories, player services, scraper, advisor, and assignment services; `ApplicationComposition` now owns the default graph and disposes long-lived resources on window close, while broader host configuration remains.
- [~] Initialize the database on startup using an explicitly temporary `EnsureCreated` plus a transactional versioned SQLite compatibility migrator and readable retryable startup error; full versioned EF migrations and deeper recovery remain.
- [~] Define database/settings paths consistently and document backup, reset, and migration behavior; timestamped SQLite backup, guarded reset, a schema-version marker, and idempotent migration results are implemented, while full versioned EF migrations remain.
- [x] Replace the current `MainWindowViewModel` demo data and legacy assignment dependency with database-backed feature view models.
- [~] Build real navigation for Home, Account Sync, Characters, Priorities, Mods, Optimizer, Thresholds, Settings, and About/Diagnostics.
- [~] Add a shared loading/empty/success/error state pattern to every data screen, including retry actions and user-visible error messages; the active data screens now expose explicit states and inline retries, while shared base abstractions and UI-state tests remain.
- [x] Remove all fabricated runtime data and make empty first-run states intentional onboarding states.
- [~] Add a small smoke-test checklist for fresh database, empty database, populated cache, Comlink unavailable, and malformed cache cases; the checklist is documented, but execution remains outstanding.

## Milestone 1: First-run account setup and reliable cache

*Exit criterion: a user can configure Comlink, enter an ally code, sync a roster, close/reopen the app, and view the cached data offline.*

- [~] Add Settings UI for Comlink URL, default ally code, theme, and reset/cache actions; guarded reset and timestamped cache backup are implemented, while import/export remains.
- [x] Validate and apply the configured Comlink URL through the actual `HttpClient` base address.
- [~] Add sync command, cancellation, bounded transient retry, and a concise result summary; progress and richer reporting remain.
- [~] Confirm the real Comlink response contract and support the complete roster plus unequipped inventory mod payload; tolerant aliases and partial-record handling now cover the known shapes, while representative real-response fixtures and metadata-backed names remain.
- [~] Persist all mod data needed by analysis: primary stat, secondary stats, roll counts, equipped owner, level, tier, pips, slot, and set.
- [x] Preserve user-owned fields such as character priorities when refreshing server-owned roster data.
- [~] Scope character, priority, mod, optimizer, and incremental scraper refreshes to the active ally code; explicit multi-account switching remains.
- [x] Make repository replacement/upsert behavior transactional and safe for repeated syncs.
- [~] Add integration tests using representative Comlink fixtures, including empty roster, inventory mods, malformed records, duplicate records, and partial responses.

## Milestone 2: Usable character and mod inventory screens

*Exit criterion: synced data is visible and useful without opening a debugger or editing files.*

- [x] Implement the Characters screen with search, roster metadata, priority display, and real empty/loading/error states.
- [~] Implement the Priorities screen with editable range validation, save/cancel behavior, dirty-state handling, and refresh after save; focused UI tests remain.
- [x] Implement the Mods screen against SQLite rather than sample objects.
- [~] Expose filters for slot, set, primary, secondary combinations, equipped status, level, pips, tier, sorting, and text search; secondary combinations now support comma-separated AND criteria and numeric comparisons, while richer controls remain.
- [~] Add stable sorting, pagination/virtualization, selected-mod detail rendering, and clear equipped-owner labels; the Mods screen now uses deterministic tie-breakers, 100-item paging, selected details, and roster-derived owner names, while explicit virtualization remains.
- [~] Convert persisted entities to domain models without dropping stats; add tests for the conversion.
- [~] Make advisor thresholds selectable and load them from persisted settings; the Mods screen now shows the active saved threshold and refreshes recommendations when it changes, while broader rule coverage remains.

## Milestone 3: Real threshold and advisor workflow

*Exit criterion: a user can define rules, evaluate real inventory mods, and understand every recommendation.*

- [~] Design the threshold storage format and migrate it from the current settings placeholder if needed; stable IDs and default selection now have backward-compatible settings fields, but a versioned settings migration remains.
- [~] Implement create, edit, duplicate, delete, select-default, validation, and versioned JSON import/export for thresholds; richer rule semantics and migration coverage remain.
- [~] Define exact upgrade/slice/sell/swap semantics, including missing stats, 5-dot slicing, set/primary compatibility, equipped ownership, and ties; the active advisor contract and focused branches are implemented, while efficiency and projected-value semantics remain.
- [~] Supply the advisor with the active ally code's cached prioritized roster and real equipped-mod context; explicit multi-account management and richer roster context remain.
- [ ] Separate recommendation calculation from UI and add table-driven tests for every decision branch.
- [~] Add explanations that identify the action, score, current values, and affected character/mod; projected-value and rule identifiers remain.

## Milestone 4: Recommendation data and optimizer

*Exit criterion: recommendations can be refreshed safely and produce deterministic, explainable, valid loadouts from the user’s actual inventory.*

- [~] Define one canonical recommendation schema shared by scraper, database, assignment service, and UI; `RecommendationSnapshot` now centralizes source, payload version, scrape time, set percentages, and per-slot primary percentages, while broader provenance remains.
- [~] Replace brittle regex-only parsing with fixture-backed parsing that tolerates current page variations, missing sections, localization, and changed markup; parsing now has a dedicated fixture-tested boundary with flexible attributes, quote styles, slot markers, localized stat text, and absent sections, while real-page fixture breadth and future markup changes remain.
- [~] Add request policy, user-agent/contact information, rate-limit handling, cancellation, stale-data policy, and per-character failure reporting; bounded retries, request headers, Retry-After handling, cancellation propagation, stale checks, and progress failures are implemented, while contact metadata and richer retry diagnostics remain.
- [~] Add a user-triggered scrape/update workflow with selected-character and all-character incremental refresh, progress, cancellation, and status; the last-run summary is now persisted, while richer retry policy remains.
- [ ] Decide whether local scraping is acceptable for release; otherwise design the central recommendation service before depending on it.
- [~] Implement deterministic six-slot assignment with no duplicate mod reuse, set-bonus rules, primary-stat matching, flexible popularity alternatives, and explicit “not enough inventory” results; the single-character contract now reports incomplete inventory and invalid set distributions, while broader alternative scoring remains.
- [~] Implement roster-wide coverage planning and swap recommendations as a single service contract; the assignment service now reserves mods once in deterministic priority order and reports inventory conflicts, while global optimization and actionable swap plans remain.
- [~] Show recommendation provenance, match reasons, alternatives, expected benefit, and stale/missing-data states in the optimizer; current/stale/missing status is visible, while provenance and detailed match explanations remain.
- [ ] Add end-to-end optimizer tests with competing mods, equipped mods, missing slots, duplicate candidates, set constraints, and community-data alternatives.

## Milestone 5: Product hardening and release readiness

*Exit criterion: another user can install, configure, use, recover from common failures, and understand the product’s limits.*

- [~] Rename `UnitTest1.cs` and split fixtures from helpers; the scraper fixture is renamed, while broader fixture organization and stale root-level duplicate cleanup remain.
- [ ] Add UI/view-model tests for navigation, state transitions, commands, validation, and no-data behavior.
- [~] Add database migration tests and a disposable test database strategy that never touches the user cache; transactional schema-version coverage and disposable SQLite fixtures exist, while a complete migration suite remains.
- [~] Add structured diagnostics/log export with privacy-safe redaction of ally codes and account payloads; the Diagnostics screen exports local environment/cache metadata, while full application log capture remains.
- [ ] Define supported OS/runtime matrix, publish profiles, self-contained versus framework-dependent packaging, and upgrade behavior.
- [~] Add crash-safe cache recovery, backup/reset tooling, and clear offline behavior; startup retry, verified backup/restore, and guarded reset now exist, while explicit offline behavior remains.
- [x] Reconcile README, architecture, state-flow, Comlink setup, file manifest, and coding standards with the shipped behavior.
- [x] Document the exact definition of "read-only" in the README, specification, and smoke-test checklist.

## Future scope after the read-only product is stable

- [ ] Central recommendation server and shared cache.
- [ ] Multiple saved loadouts per character.
- [ ] Grand Arena multi-squad planning.
- [ ] Dashboards and data visualizations.
- [ ] Theme customization beyond the initial theme setting.
- [ ] Any write-access feature only after a separate security/authentication design, threat model, consent flow, audit trail, and rollback plan.
