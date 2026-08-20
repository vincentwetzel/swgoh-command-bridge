# TODO Roadmap — Release Readiness

Audit date: 2026-08-20

## What is actually left

The implementation audit is complete. There are no partially completed roadmap items. The remaining work is release verification or deliberately deferred product scope.

## Release gates — required before publishing

- [ ] Execute [`docs/SMOKE_TEST_CHECKLIST.md`](docs/SMOKE_TEST_CHECKLIST.md) against a built release on the primary Windows target and record the result.
- [ ] Verify framework-dependent and self-contained published artifacts on each supported target before publishing.

These require packaged builds and runtime verification, which are intentionally left to the release operator.

## Future product scope — not required for the read-only release

- [ ] Add the priority-weighted mod-farming recommender: rank mod sets and slot/primary combinations by roster need, character priority, and relative mod-quality weakness.
- [ ] Add authenticated Comlink support to the preferred-mod publisher, or run it on infrastructure that can safely reach an authenticated endpoint.
- [ ] Complete legal/release-policy review for public recommendation scraping and deployment authentication.
- [ ] Support multiple saved loadouts per character.
- [ ] Add Grand Arena multi-squad planning.
- [ ] Add dashboards and data visualizations.
- [ ] Add unbounded roster solving and full game-stat simulation beyond persisted mod-stat projections.
- [ ] Expand theme customization beyond the initial theme setting.
- [ ] Design any write-access feature separately, including security/authentication, threat model, consent, audit trail, and rollback requirements.

## Current product boundary

The current target is a local, read-only desktop product. It can configure Comlink, sync and cache player data by ally code, display catalog-backed portraits and gear/relic frames, analyze persisted mods, retrieve recommendations under a release-controllable scraping policy, and produce deterministic explainable assignments. Windows x64 can manage the local Comlink runtime; Linux/macOS deployments currently require an external Comlink service.

It does not write to the game, provide full game-stat simulation, or claim legal approval for public scraping.

Known release assumptions:

- Windows x64 is the primary supported target and the only target with automatic Comlink installation; Linux/macOS remain candidate targets until their published artifacts, external-Comlink workflow, and smoke tests are verified.
- Build, test, visual smoke, and packaged-runtime verification are release-operator responsibilities.
- A generic hosted-worker architecture is not required for this desktop product.

## Supporting documents

- [`docs/ROADMAP_HISTORY.md`](docs/ROADMAP_HISTORY.md) — completed implementation audit.
- [`docs/SMOKE_TEST_CHECKLIST.md`](docs/SMOKE_TEST_CHECKLIST.md) — manual release verification procedure.
- [`docs/RELEASE_GUIDE.md`](docs/RELEASE_GUIDE.md) — targets, publishing, recovery expectations, and release gates.

## Maintenance rules

- Keep this file focused on work that remains to be done.
- Keep deferred ideas under **Future product scope** rather than mixing them with release blockers.
- When a release gate is completed, record the target, artifact/version, date, and result in the release documentation before removing it here.
- Add newly discovered work here only when it has a concrete next action and a clear release or future-scope classification.
