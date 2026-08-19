# Release Guide

This document defines the release targets and packaging policy for the read-only desktop application. It describes the commands the release operator should run; this repository change does not execute builds or publish artifacts. The UI/Core code can target multiple desktop platforms, while automatic Comlink installation is currently Windows x64-only.

The implementation audit is complete. Release readiness still requires a successful solution build/test run, target-specific publish verification, and the manual scenarios in [SMOKE_TEST_CHECKLIST.md](SMOKE_TEST_CHECKLIST.md). Record those results with the artifact version and target before removing the corresponding gates from `TODO.md`.

## Runtime matrix

| Target | Runtime/package target | Status for this milestone | Comlink setup |
|---|---|---|---|
| Windows x64 | `win-x64`, .NET 8 | Primary target | Managed Comlink setup uses pinned releases `4.4.0`, then `4.2.0`. The application manifest declares Windows 10 compatibility; verify Windows 10 and 11. |
| Linux x64 | `linux-x64`, .NET 8 | Candidate target | Provide and verify an externally managed Comlink endpoint; also verify desktop dependencies and executable permissions. |
| macOS x64 | `osx-x64`, .NET 8 | Candidate target | Provide and verify an externally managed Comlink endpoint; also verify windowing, local application-data paths, and Gatekeeper behavior. |
| macOS arm64 | `osx-arm64`, .NET 8 | Candidate target | Provide and verify an externally managed Comlink endpoint; also verify native SQLite/Avalonia runtime assets on Apple Silicon. |

The application must remain read-only against the game account on every target. Local SQLite and JSON cache/settings files are the only intended writes.

## Framework-dependent publish

Use this mode when the target machine is managed and has the matching .NET 8 desktop runtime installed. The examples use POSIX-style line continuation; in PowerShell, put the command on one line or replace each trailing `\` with a backtick:

```text
dotnet publish src/swgoh-command-bridge.UI/swgoh-command-bridge.UI.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained false \
  --output artifacts/publish/win-x64-framework-dependent
```

Replace `win-x64` with `linux-x64`, `osx-x64`, or `osx-arm64` after that target passes the manual smoke checklist. Non-Windows packages must document the externally managed Comlink prerequisite.

## Self-contained publish

Use this mode for users who should not install .NET separately. It produces a larger artifact and still requires platform-specific manual verification. The same PowerShell line-continuation note applies:

```text
dotnet publish src/swgoh-command-bridge.UI/swgoh-command-bridge.UI.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  --output artifacts/publish/win-x64-self-contained
```

Trimming stays disabled until a release-specific test confirms that Avalonia XAML, EF Core, SQLite, and the view-model locator survive trimming. Single-file packaging is also deferred until native runtime loading has been verified for each target.

## Upgrade and recovery policy

1. Before upgrading, use Settings to create a timestamped cache backup.
2. Keep the existing `SWGOHCommandBridge` application-data directory. Settings, cache, diagnostics, backups, and the cached preferred-mod dataset share this case-stable platform-local directory and are not part of the published output.
3. On first launch after an upgrade, startup applies the transactional compatibility pass and records the resulting schema version.
4. Confirm existing player sync timestamps, bounded sync history, recommendation provenance, and settings values survive the compatibility pass.
5. If startup cannot initialize the cache, use the visible retry action, restore a verified backup, or reset the cache. Reset preserves JSON settings.
6. A backup created by a newer unsupported schema is rejected before it can replace the active cache.
7. Do not delete the previous release or its application-data directory until the upgraded build passes the smoke checklist.

There is no automatic in-place rollback. The release operator owns artifact retention and should preserve the previous installer/publish directory until upgrade verification is complete.

## Release gate

The following are required before publishing an artifact:

- `dotnet restore`, build, and test pass for the solution.
- The release build/test commands use a writable temporary directory when the host environment does not permit MSBuild to write to its default temp location.
- The target-specific publish command completes without warnings that affect runtime loading.
- The fresh-install, populated-cache, offline, malformed-cache, backup/restore, and read-only scenarios in [SMOKE_TEST_CHECKLIST.md](SMOKE_TEST_CHECKLIST.md) pass.
- The published directory contains the UI executable, Core assembly, Avalonia assets, SQLite provider assets, and all required runtime files.
- Windows documentation identifies the managed Comlink release sequence and target architecture; non-Windows documentation identifies the external Comlink prerequisite.
- The embedded preferred-mod baseline opens safely offline, and a verified published dataset is exercised through the Characters smoke checks when one is available.

## Preferred-mod data publishing

The preferred-mod dataset has its own cadence and does not block a desktop release. After the workflow has been pushed to GitHub:

1. Host a Comlink endpoint reachable from GitHub Actions; a local `localhost` endpoint will not work.
2. Add its URL as the repository Actions secret `COMLINK_BASE_URL`.
3. Run **Refresh preferred mods** manually once and verify that it commits only `data/preferred-mods/dataset.json` and `manifest.json`.
4. Confirm a fresh app startup downloads the dataset and an offline restart retains it.

The committed bootstrap dataset is intentionally empty until the first successful refresh. The current publisher does not support Comlink access-key/secret-key authentication; do not place credentials in the URL or repository until that support exists.
