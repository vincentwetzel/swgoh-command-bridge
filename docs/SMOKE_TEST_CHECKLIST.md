# Smoke Test Checklist

Use this checklist after restoring/building the solution. Record the date, OS, runtime version, and result for each scenario before calling a build release-ready.

Run `dotnet build swgoh-command-bridge.sln` and `dotnet test swgoh-command-bridge.sln` before the manual scenarios. A failed build/test run is a release blocker even when the UI scenarios pass.

## Fresh install

- [ ] Start with no existing application-data cache or settings file.
- [ ] Launch the application and confirm the shell opens without fabricated roster/mod data.
- [ ] Confirm the local SQLite cache is created and empty states are readable.
- [ ] Open Characters and confirm preferred-mod guidance handles the bundled bootstrap dataset without errors.
- [ ] Open Settings, enter a valid Comlink URL and ally code, save, close, and reopen the app.
- [ ] On Linux/macOS candidate builds, configure a healthy externally managed Comlink endpoint and confirm the app does not assume the Windows managed runtime is available.

## Empty and populated cache

- [ ] With an empty cache, verify Characters, Priorities, Mods, and Optimizer show intentional empty states.
- [ ] Sync a test account and confirm the result summary reports useful counts.
- [ ] Relaunch with the selected account cache older than 24 hours and confirm cached data appears immediately while a background refresh starts and reports progress in the account dropdown.
- [ ] Relaunch with a fresh selected account cache and confirm startup does not issue another account sync.
- [ ] Confirm the latest sync result and warning count appear in the account dropdown and Diagnostics.
- [ ] Confirm Diagnostics lists recent redacted sync attempts without full ally codes or account payloads.
- [ ] Confirm characters, priorities, mods, equipped owners, and optimizer data refresh without restarting.
- [ ] Open Priorities, verify character and ship tabs show only their respective units, drag units between S/A/B/C/D and Unranked, reorder units within a tier, save, restart, and confirm placement/order persist.
- [ ] Sync the same account again and confirm the priority board retains local tier placement and ordering.
- [ ] Confirm startup catalog refresh is best-effort: localized character names, bundled portraits, and alignment-aware gear/relic frames appear when catalog data is available, while an unavailable Comlink leaves the embedded/previous catalog usable.
- [ ] Confirm a character without a matching portrait asset shows initials, and that the shared portrait rendering is consistent in Characters, Priorities, and Optimize.
- [ ] Close and reopen the app with Comlink unavailable; confirm cached data remains viewable offline.
- [ ] With a stale cache and Comlink unavailable, confirm the background refresh fails visibly while the previous cached roster and mods remain usable.
- [ ] Select a different cached account from the account dropdown and confirm Characters, Mods, Priorities, and Optimizer remain scoped to that account without a Comlink request.
- [ ] Remove a selected cached account only after confirmation and confirm its character/mod data is gone while other cached accounts remain.

## Comlink and input failures

- [ ] On a clean Windows x64 user profile without Docker, launch the app and confirm the managed Comlink setup shows download/startup progress and reaches a ready state.
- [ ] Close the app and confirm the managed Comlink process is no longer running; relaunch and confirm the installed version is reused without downloading again.
- [ ] On Windows x64 with an already healthy local Comlink endpoint, confirm the app reuses it and does not start a second managed process.
- [ ] Use an unreachable Comlink URL and confirm Test Connection and Sync show actionable errors.
- [ ] Exercise cancellation during a sync and confirm the UI returns to an idle/retryable state.
- [ ] Enter an invalid URL and confirm Settings rejects it without changing the active client URL.
- [ ] Change the theme to Dark, Light, and System, then restart and confirm the selected theme is applied.
- [ ] Load malformed or partial fixture data and confirm the app reports the affected operation instead of crashing.
- [ ] Verify malformed/impossible mod primary pairs are corrected when unambiguous or shown as unavailable, and do not produce invalid preferred-mod guidance.
- [ ] Verify an existing cache migrates through schema version 11 and preserves character priority placement, alignment, and relic tier data.

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
- [ ] Exercise a transient recommendation failure or rate limit and confirm bounded retry/backoff behavior and a useful refresh summary.
- [ ] Disable local recommendation scraping in Settings, confirm cached recommendations remain readable, and confirm refresh actions explain that new scraping is disabled.
- [ ] With a published preferred-mod dataset available, verify Characters shows a preferred set pattern and slot-primary guidance for a represented character.
- [ ] Verify a close primary split is shown as a viable alternative rather than a hard mismatch, and an empty slot still receives guidance.
- [ ] Disconnect the network, restart, and confirm the last verified preferred-mod dataset remains readable.
