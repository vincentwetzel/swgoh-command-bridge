# Preferred Mods Publisher

This tool fetches the top 50 accounts from each Kyber GAC division through ComLink, downloads their public roster payloads, and writes only an aggregate preferred-mod dataset. It never writes raw player profiles or ally codes to disk.

Required environment variable:

```text
COMLINK_BASE_URL=https://your-comlink-host
```

Optional environment variables:

- `PREFERRED_MODS_MAX_ACCOUNTS` (default `250`)
- `PREFERRED_MODS_MIN_PROFILES` (default `100`)
- `PREFERRED_MODS_CONCURRENCY` (default `5`)
- `PREFERRED_MODS_GAC_DIVISIONS` (default `25,20,15,10,5`)
- `PREFERRED_MODS_DATASET_URL` (the public raw GitHub dataset URL)

Run locally with:

```text
dotnet run --project tools/swgoh-command-bridge.PreferredModsPublisher -- data/preferred-mods
```
