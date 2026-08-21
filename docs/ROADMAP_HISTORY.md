# Roadmap History — Completed Implementation Audit

Audit date: 2026-08-20

This document records the implementation work verified during the roadmap audit. It is intentionally separate from [`TODO.md`](../TODO.md), which contains only remaining work.

## Foundation and startup

- Core, UI, and test projects are structured with EF Core/SQLite, Avalonia, and xUnit.
- The explicit composition root wires settings, database, logging, HTTP/Comlink, repositories, sync, scraper, advisor, and assignment services.
- Startup creates or repairs the local schema through a transactional, versioned SQLite compatibility migrator with retryable errors, rollback, backup, restore, and isolated migration coverage.
- Cache, settings, diagnostics, and backup paths are centralized and documented.
- Windows x64 startup can reuse a healthy local Comlink, download pinned compatible releases, verify supplied digests, report progress, and stop the process owned by the application; other platforms use an external endpoint.
- Runtime sample data and legacy demo dependencies were removed.

## Shell, navigation, and state

- Home, Characters, Priorities, Mods, Optimizer, Thresholds, Settings, and Diagnostics use real navigation.
- Data screens provide intentional first-run empty states and explicit loading, success, error, and retry states.
- Shared view-model state transitions and shell commands have focused coverage.
- Settings supports Comlink URL, ally code, theme, cache actions, and versioned import/export.
- Visual verification remains part of the release smoke checklist.

## Account sync and cache

- The top-right account switcher validates ally codes and supports adding accounts, sync progress, cancellation, bounded retry, failure categorization, parser warnings, and retry.
- Comlink mapping supports bounded envelopes, known aliases, nested metadata, complete roster/inventory records, duplicate suppression, and record-level diagnostics.
- Persisted mod snapshots retain primary/secondary stats, roll counts, ownership, level, tier, pips, slot, and set.
- Character, priority, mod, optimizer, recommendation, and scraper data are scoped to the active ally code.
- Cached accounts can be selected, searched, switched offline, and removed with confirmation.
- Startup can refresh one stale active account after cached data loads without blocking offline access to the previous cache.
- Character catalogs use embedded verified JSON, best-effort Comlink refresh, localized names, portrait assets, audit summaries, and atomic persistence with rollback to the prior snapshot.
- Character records persist normalized alignment and relic tier data; shared UI portrait rendering selects gear/relic highlights and falls back to initials when an asset is unavailable.
- Priority management uses a separate character/ship tier board with S/A/B/C/D and Unranked rows, drag-and-drop ordering, bundled-catalog ship classification, and preservation across account replacement.
- Mod-primary mapping validates each shape's legal primaries and repairs known legacy identifiers during parsing and cache migration.
- Mod presentation uses linked game artwork and a versioned visual spec to render shape-specific chassis, tier-colored set emblems, rarity pips, and six-dot variants in Characters, Mods, and Optimize; an offline preview switch covers all six shapes.
- Repository replacement, upsert, account removal, and failed-refresh rollback are transactional.
- Representative Comlink fixtures cover empty, malformed, duplicate, partial, nested, and inventory-heavy responses.

## Characters, priorities, and inventory

- Characters and Priorities are database-backed with search, metadata, validation, save/cancel, dirty-state, refresh, and no-data behavior.
- Mods supports SQLite-backed filtering by text, slot, set, primary, secondary combinations, equipped status, level, pips, tier, sorting, result counts, and reset.
- Mods provides deterministic sorting, paging, virtualization, owner labels, selected details, and readable quality/set-slot/stat summaries.
- Persisted entities map to domain models without dropping supported stats or tolerantly failing malformed stat values.
- Advisor thresholds are loaded from persisted settings, selectable in the Mods workflow, and applied when selected-mod analysis refreshes.

## Thresholds and advisor

- Thresholds have stable IDs, duplicate/edit/delete/default operations, finite-value validation, and versioned JSON import/export.
- Legacy threshold settings migrate into the versioned settings envelope with a valid default.
- Upgrade, slice, sell, keep, and swap semantics cover rarity, level, tier, slice, missing stats, 5-dot behavior, set/primary compatibility, ownership, ties, and efficiency.
- Advisor results include deterministic action ordering, score/reason details, affected character/mod, source threshold ID/name, current/projected efficiency, and explicit limitations.
- Calculation is separated from UI and covered by decision-matrix, tie-break, persisted-inventory, and UI-handoff tests.

## Recommendations and optimizer

- Scraper, database, assignment service, and UI share a canonical account-scoped recommendation schema.
- Recommendation parsing handles page noise, duplicate sections, localization, changed markup, missing sections, flexible attributes, nested markup, and absent values.
- Scraping has configurable contact metadata, bounded responses, cancellation, stale-data checks, rate-limit handling, retry/backoff/pacing, incremental refresh, progress, and per-character failure reporting.
- Local scraping can be disabled while cached recommendations remain readable; legal/release approval remains future scope.
- Single-character assignment is deterministic, prevents duplicate mod reuse, applies set/primary popularity and rule constraints, reports alternatives/conflicts, and identifies equipped swaps.
- Roster planning supports priority-first and bounded joint optimization, reservation/conflict reporting, cancellation, and large-roster fallback.
- Optimizer UI exposes provenance, stale/missing states, explanations, alternatives, swap candidates, projected persisted-stat impact, and conflict summaries.
- Fixtures and tests cover competing mods, equipped mods, missing slots, duplicates, set constraints, reserved inventory, invalid six-slot distributions, and roster conflicts.

## Preferred GAC mod data

- A global preferred-mod dataset models high-ranking GAC equipped-mod trends by character, including complete set patterns, slot-primary distributions, viable alternatives, confidence, and aggregate speed-quality profiles.
- The desktop app embeds a bootstrap baseline, caches a validated last-known-good dataset offline, and silently refreshes it from a GitHub-hosted manifest without requiring the user's Comlink endpoint.
- Characters shows prescriptive set/primary guidance for equipped and empty slots while remaining permissive for close top-player usage splits.
- A maintainer-run publisher queries local ComLink, aggregates a few hundred GAC accounts, validates the result, and commits only aggregate data and its manifest; it does not publish raw profiles or ally codes.

## Hardening and documentation

- Named fixtures replace the old test placeholder and keep payload data separate from test helpers.
- View-model, navigation, command, validation, no-data, backup/restore failure, threshold failure, and diagnostics retry coverage exists.
- Migration coverage includes the supported legacy schema matrix, repair, rollback, idempotence, unsupported versions, backup, restore, mod-primary correction, and priority-tier migration using isolated databases.
- The cache compatibility migrator is now at schema version 11, with migrations for character alignment and persisted relic tiers.
- Diagnostics capture bounded privacy-safe events and redact ally codes/account payloads.
- Character catalog parser, refresh, fallback, and UI portrait/name behavior are covered by the current source and tests.
- Mod visual spec parsing, coordinate conversion, tier palette, and layered layout behavior are covered by the current source/tests; packaged asset presence remains a release smoke-test responsibility.
- Crash-safe cache recovery, guarded reset, verified restore rollback, and offline behavior are implemented.
- README, architecture, state-flow, Comlink setup, diagnostics, file manifest, release guide, and coding standards reflect the shipped behavior and platform support boundary.
- The read-only boundary is documented in the README, specification, and smoke checklist.
