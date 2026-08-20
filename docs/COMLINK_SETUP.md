# Setting Up swgoh-comlink

To avoid dealing with Protobuf formatting and authentication handshakes, this application communicates with a configured instance of `swgoh-comlink` over HTTP. The default is a local endpoint; externally managed local or remote endpoints are also supported.

## Automatic setup

The desktop application manages the normal local Comlink setup for you on Windows x64. At startup it checks the configured endpoint first. If a local endpoint is not healthy, it downloads a Windows executable from the pinned compatible release sequence `4.4.0`, then `4.2.0`, into the application's local AppData directory, verifies a supplied SHA-256 release digest, starts the executable as a hidden child process, and waits for its HTTP endpoint to become ready. The startup status is shown in the application while this happens. The process is stopped when the application closes, and cached account data remains available if setup cannot complete.

The managed runtime currently supports 64-bit Windows only. The Avalonia application can be published for Linux or macOS, but those deployments must point Settings at a healthy externally managed local or remote Comlink service. A healthy service already listening on the configured Windows localhost port is also reused without downloading or starting a second process.

No Docker installation or command-line setup is required for the normal desktop workflow.

## Quick Start with Docker

Docker remains available for developers or users who intentionally manage Comlink outside the application. Run the following command to start the service locally:

```bash
docker run -d \
  -p 3000:3000 \
  --name swgoh-comlink \
  -e "ACCESS_KEY=your_optional_access_key" \
  -e "SECRET_KEY=your_optional_secret_key" \
ghcr.io/swgoh-utils/swgoh-comlink:latest
```

Verify that the container is running with `docker ps`. The application uses the proxy's HTTP API; it does not connect directly to game servers. If a healthy Comlink is already listening on the configured localhost URL, the application uses it instead of starting a second process.

The application defaults to `http://localhost:3000`. You can change the Comlink URL from the Settings screen and use Test Connection before syncing an account. Requests identify the client and request JSON responses; if a deployment places authentication in front of Comlink, provide it at that deployment boundary rather than in this application or its settings/transfer files. Sync accepts nine-digit ally codes and reports tolerated malformed or duplicate payload records after parsing, including malformed equipped-mod records and nested character display metadata when the response supplies them. It attempts catalog enrichment first, resolving localized character names and portrait assets from Comlink game-data/localization payloads; if that is unavailable it falls back to the last verified local catalog, then the player payload and metadata endpoint. Catalog refresh is best-effort and never discards the previous verified snapshot. Recommendation caches are scoped by both character and ally code, so switching cached accounts cannot reuse another account's recommendation row.

## Preferred-mod publisher

Preferred-mod publication is a maintainer task performed on a local PC. Start local ComLink, then run:

```powershell
dotnet run --project tools/swgoh-command-bridge.PreferredModsPublisher -- data/preferred-mods
```

The publisher defaults to `http://localhost:3000`; set the local `COMLINK_BASE_URL` environment variable only when the local service uses a different URL. It samples high-ranking GAC accounts, writes only aggregate mod trends to `data/preferred-mods/`, and never writes raw profiles or ally codes to disk. Review and commit the generated `dataset.json` and `manifest.json`, then push them to GitHub. Desktop clients silently download those public aggregate files and cache them offline.

No GitHub Actions secret, hosted ComLink service, or user-side bulk profile download is required. Do not put ComLink access keys, session values, or credentials in source control, the environment URL, command output, or dataset files.

## Notes
- Keep this service local; SWGOH Command Bridge is currently scoped as a read-only analysis tool.
- Do not commit access keys, session values, or other account credentials.
- If the container already exists, restart it with `docker start swgoh-comlink`.
- If the container was created with credentials, do not put those values in the repository or in screenshots. Stop and recreate the container to change them.
- Player and metadata requests retry a small number of times for transient transport failures and HTTP 408, 429, and 5xx responses. Permanent client errors are reported immediately. A failed or cancelled sync preserves the previous successful account cache and records the outcome in bounded local sync history.
- Community recommendation requests identify the client, optionally send the configured Settings contact email as HTTP `From` metadata, reject invalid contact values, use bounded retries and a 2 MiB response-size guard, honor server-provided rate-limit delays, propagate user cancellation, and report privacy-safe endpoint/failure reasons to the optimizer. The optimizer keeps a seven-day freshness window and records the last refresh summary. Public scraping legal/release review remains a separate roadmap decision.
- Local community scraping is an explicit Settings policy. Disabling it prevents new `swgoh.gg` requests while preserving cached recommendation data; it is independent of the bundled/cached global preferred-mod dataset.
- Preferred-mod data is a separate global aggregate cache, not a copy of the user's account. The app retains its embedded or last verified dataset offline, and the publisher must never commit raw profiles or ally codes.
- The local SQLite cache is created on first launch. Existing compatible caches receive missing tables, priority-tier/order columns, player sync-freshness columns, sync-history tables, mod columns, recommendation provenance columns, character alignment, persisted relic tiers, and known invalid-primary repairs through a transactional, idempotent compatibility migrator; the current cache schema is version 11. The migrator is tested across every supported legacy version, partial tables, malformed migration rollback, and unsupported future versions using disposable databases. Settings includes timestamped SQLite backup and integrity-checked restore commands; restore is limited to the cache backup directory, unsupported future-schema backups are rejected before replacement, and guarded reset preserves settings. Thresholds and application settings can be transferred through versioned JSON controls; persisted settings also use a versioned envelope and migrate legacy threshold records to stable IDs/default selection. Embedded URL credentials are excluded. Richer profile management remains future scope.

## Troubleshooting

- **Test Connection fails:** On Windows x64, retry startup with an internet connection and check that security software allows the downloaded Comlink executable to run. On Linux/macOS, confirm an external Comlink service is running before testing. For any externally managed service, confirm the configured URL includes the correct port and no firewall or proxy blocks it. HTTP 404 indicates an endpoint/version mismatch; HTTP 401/403 indicates a service configuration issue.
- **Sync fails:** Follow the categorized message in the account switcher and check the Comlink logs when indicated. A failed sync does not intentionally replace the last successful cache. If a startup stale-cache refresh fails, the same previous cache remains available and the failure is recorded in sync status/history.
- **The cache cannot open:** Use Diagnostics to capture the error and Settings to restore a verified backup or reset cached feature data. Reset does not remove JSON settings.
- **Recommendation refresh is empty or partial:** This can indicate missing character cache data, stale or missing public pages, cancellation, or per-character request failures. Review the refresh summary and retry selectively.
