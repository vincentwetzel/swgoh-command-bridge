# Smoke Test Checklist

Use this checklist after restoring/building the solution. Record the date, OS, runtime version, and result for each scenario before calling a build release-ready.

Run `dotnet build swgoh-command-bridge.sln` and `dotnet test swgoh-command-bridge.sln` before the manual scenarios. A failed build/test run is a release blocker even when the UI scenarios pass.

## Fresh install

- [ ] Start with no existing application-data cache or settings file.
- [ ] Launch the application and confirm the shell opens without fabricated roster/mod data.
- [ ] Confirm the local SQLite cache is created and empty states are readable.
- [ ] Open Settings, enter a valid Comlink URL and ally code, save, close, and reopen the app.

## Empty and populated cache

- [ ] With an empty cache, verify Characters, Priorities, Mods, and Optimizer show intentional empty states.
- [ ] Sync a test account and confirm the result summary reports useful counts.
- [ ] Confirm characters, priorities, mods, equipped owners, and optimizer data refresh without restarting.
- [ ] Close and reopen the app with Comlink unavailable; confirm cached data remains viewable offline.
- [ ] Select a different cached account from Home and confirm Characters, Mods, Priorities, and Optimizer remain scoped to that account without a Comlink request.
- [ ] Remove a selected cached account only after confirmation and confirm its character/mod data is gone while other cached accounts remain.

## Comlink and input failures

- [ ] Use an unreachable Comlink URL and confirm Test Connection and Sync show actionable errors.
- [ ] Exercise cancellation during a sync and confirm the UI returns to an idle/retryable state.
- [ ] Enter an invalid URL and confirm Settings rejects it without changing the active client URL.
- [ ] Load malformed or partial fixture data and confirm the app reports the affected operation instead of crashing.

## Cache recovery

- [ ] Create a cache backup from Settings and confirm a timestamped file appears under the cache `backups` directory.
- [ ] Confirm the backup path is shown in Settings and the backup can be copied independently.
- [ ] Restore a backup from the cache `backups` directory and confirm the cached records return after reload.
- [ ] Confirm reset requires explicit confirmation, preserves Settings values, and clears cached feature data.
- [ ] Confirm startup retry is visible when cache initialization fails.

## Read-only boundary

- [ ] Confirm no screen exposes a command that writes to the game account or changes game data.
- [ ] Confirm ally codes, access keys, and account payloads are not written to logs or diagnostics output.
- [ ] Confirm a recommendation refresh can be cancelled and leaves the previous valid cache intact.
- [ ] Confirm stale or missing `swgoh.gg` data is labeled and does not silently appear as current.
- [ ] Disable local recommendation scraping in Settings, confirm cached recommendations remain readable, and confirm refresh actions explain that new scraping is disabled.
