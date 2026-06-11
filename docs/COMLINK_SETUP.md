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

The application expects the proxy to be reachable at `http://localhost:3000` unless configuration support is added later.

## Notes
- Keep this service local; SWGOH Command Bridge is currently scoped as a read-only analysis tool.
- Do not commit access keys, session values, or other account credentials.
- If the container already exists, restart it with `docker start swgoh-comlink`.
