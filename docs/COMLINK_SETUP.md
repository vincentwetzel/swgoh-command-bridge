# Setting Up swgoh-comlink

To avoid dealing with Protobuf formatting and authentication handshakes, this application communicates with a local instance of `swgoh-comlink` over localhost.

## Quick Start with Docker

Run the following command to start the Comlink service locally:

```bash
docker run -d \
  -p 3000:3000 \
  --name swgoh-comlink \
  -e "ACCESS_KEY=your_optional_access_key" \
  -e "SECRET_KEY=your_optional_secret_key" \
progresso/swgoh-comlink:latest
```

Verify that the container is running with `docker ps`. The application uses the proxy's HTTP API; it does not connect directly to game servers.

The application defaults to `http://localhost:3000`. You can change the Comlink URL from the Settings screen and use Test Connection before syncing an account.

## Notes
- Keep this service local; SWGOH Command Bridge is currently scoped as a read-only analysis tool.
- Do not commit access keys, session values, or other account credentials.
- If the container already exists, restart it with `docker start swgoh-comlink`.
- If the container was created with credentials, do not put those values in the repository or in screenshots. Stop and recreate the container to change them.
- Player and metadata requests retry a small number of times for transient transport failures and HTTP 408, 429, and 5xx responses. Permanent client errors are reported immediately.
- Community recommendation requests identify the client, use bounded retries, honor server-provided rate-limit delays, and propagate user cancellation. The optimizer keeps a seven-day freshness window and records the last refresh summary; contact metadata and parser hardening remain roadmap work.
- The local SQLite cache is created on first launch. Existing caches receive compatible mod-stat columns and recommendation provenance columns through a transactional, idempotent compatibility migrator and record a supported cache schema version. Settings includes timestamped SQLite backup and integrity-checked restore commands plus a guarded reset that preserves settings. Thresholds can be transferred through the versioned JSON controls on the Thresholds screen; full settings-wide import/export and EF migration history are still planned.

## Troubleshooting

- **Test Connection fails:** Confirm the container is running, the configured URL includes the correct port, and no firewall or proxy blocks localhost.
- **Sync fails:** Check the ally code and Comlink logs, then retry. A failed sync does not intentionally replace the last successful cache.
- **The cache cannot open:** Use Diagnostics to capture the error and Settings to restore a verified backup or reset cached feature data. Reset does not remove JSON settings.
- **Recommendation refresh is empty or partial:** This can indicate missing character cache data, stale or missing public pages, cancellation, or per-character request failures. Review the refresh summary and retry selectively.
