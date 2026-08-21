# Diagnostics and Support

The Diagnostics screen is intended to make local support and recovery checks observable without exposing account data.

It reports:

- the local SQLite cache and backup locations;
- the settings file location;
- the application event-log, cache, and backup locations;
- the Comlink authority only, without URL credentials or paths;
- a redacted ally code showing only the last four digits;
- aggregate counts for players, characters, mods, and recommendations; and
- the persisted recommendation refresh summary;
- the latest sync outcome and ten most recent redacted sync attempts; and
- a bounded list of recent privacy-safe application events from the UI and Core services.

Export creates a timestamped text report under the application's local `diagnostics` directory. Reports intentionally exclude full ally codes, account payloads, access keys, and session values. Review a report before sharing it.

Diagnostics is observational. It does not send account payloads, repair the cache, or change Comlink settings. Refresh only rereads local metadata, bounded sync outcome history, and event state. Use Settings for backup, restore, reset, settings transfer, and configuration changes.

Mod-art loading is outside the account diagnostics boundary. If a chassis or set-emblem resource is missing, the visual control shows a bounded missing-art message in the affected view; use the offline `--mod-visual-preview` window to distinguish an asset-packaging problem from a cache or Comlink problem. The preview does not write diagnostics or initialize account services.

Comlink startup failures are reported through the shell startup status and bounded application events. Account-sync failures, including a stale-cache refresh started during startup, are reported in the account switcher and bounded sync history. Catalog and preferred-mod refreshes are best-effort; failures are recorded in the bounded event log while the last verified or embedded data remains active. Diagnostics does not expose downloaded executable contents, process output, catalog payloads, preferred-mod payloads, or account payloads. On Windows x64, a managed runtime is stored under the documented application-data directory; on other platforms, the configured service is external to this application.

Diagnostics shows the latest sync outcome plus the ten most recent account attempts. Ally codes are redacted in the display and export; statuses, counts, parser-warning totals, cancellation/interruption state, and privacy-safe failure summaries are retained for support.

The initial bundled preferred-mod dataset is intentionally a bootstrap baseline. If it remains empty, a maintainer must run the local publisher against their local ComLink, commit `data/preferred-mods/`, and push it to GitHub. Then restart the app or wait for its next silent startup check. Offline use keeps the last verified dataset. An invalid manifest or download is rejected without replacing that copy; update details are available through application events rather than a dedicated Diagnostics dataset panel.

For a support report, include the report's timestamp, operating system, .NET runtime, and the operation that failed. Do not attach `cache.db`, a backup database, `settings.json`, or Comlink credentials unless they have been separately sanitized.
