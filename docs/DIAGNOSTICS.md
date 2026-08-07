# Diagnostics and Support

The Diagnostics screen is intended to make local support and recovery checks observable without exposing account data.

It reports:

- the local SQLite cache and backup locations;
- the settings file location;
- the Comlink authority only, without URL credentials or paths;
- a redacted ally code showing only the last four digits;
- aggregate counts for players, characters, mods, and recommendations; and
- the persisted recommendation refresh summary; and
- a bounded list of recent privacy-safe application events from the UI and Core services.

Export creates a timestamped text report under the application's local `diagnostics` directory. Reports intentionally exclude full ally codes, account payloads, access keys, and session values. Review a report before sharing it.

Diagnostics is observational. It does not send account payloads, repair the cache, or change Comlink settings. Refresh only rereads local metadata and event state. Use Settings for backup, restore, reset, settings transfer, and configuration changes.

For a support report, include the report's timestamp, operating system, .NET runtime, and the operation that failed. Do not attach `cache.db`, a backup database, `settings.json`, or Comlink credentials unless they have been separately sanitized.
