# Diagnostics and Support

The Diagnostics screen is intended to make local support and recovery checks observable without exposing account data.

It reports:

- the local SQLite cache and backup locations;
- the settings file location;
- the verified character-catalog location and catalog refresh status;
- the Comlink authority only, without URL credentials or paths;
- a redacted ally code showing only the last four digits;
- aggregate counts for players, characters, mods, and recommendations; and
- the persisted recommendation refresh summary;
- the latest sync outcome and ten most recent redacted sync attempts; and
- a bounded list of recent privacy-safe application events from the UI and Core services.

Export creates a timestamped text report under the application's local `diagnostics` directory. Reports intentionally exclude full ally codes, account payloads, access keys, and session values. Review a report before sharing it.

Diagnostics is observational. It does not send account payloads, repair the cache, or change Comlink settings. Refresh only rereads local metadata, bounded sync outcome history, and event state. Use Settings for backup, restore, reset, settings transfer, and configuration changes.

Comlink startup failures are reported through the shell startup status and bounded application events. Account-sync failures, including a stale-cache refresh started during startup, are reported in the account switcher and bounded sync history. Catalog refresh failures are best-effort and leave the last verified embedded or persisted catalog active. Diagnostics identifies the configured authority but does not expose downloaded executable contents, process output, catalog payloads, or account payloads. On Windows x64, a managed runtime is stored under the documented application-data directory; on other platforms, the configured service is external to this application.

Diagnostics shows the latest sync outcome plus the ten most recent account attempts. Ally codes are redacted in the display and export; statuses, counts, parser-warning totals, cancellation/interruption state, and privacy-safe failure summaries are retained for support.

For a support report, include the report's timestamp, operating system, .NET runtime, and the operation that failed. Do not attach `cache.db`, a backup database, `settings.json`, or Comlink credentials unless they have been separately sanitized.
