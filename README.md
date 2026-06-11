# SWGOH Command Bridge

A cross-platform desktop application written in C# and Avalonia UI for read-only SWGOH roster, mod inventory, and mod optimization analysis.

## Current Shape
- `src/swgoh-command-bridge.Core` contains the compiled domain models, mod advisor/assignment services, and the `swgoh.gg` scraper service.
- `src/swgoh-command-bridge.UI` contains the Avalonia shell, navigation, and feature viewmodels for characters, mods, priorities, thresholds, and optimization.
- Local SQLite persistence is represented by EF Core database/entity drafts at the repository root. UI viewmodels and Core services currently depend on those namespaces while the files are being consolidated into the compiled Core project.
- `tests/swgoh-command-bridge.Tests` is the test project scaffold.

## Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) to run the local `swgoh-comlink` proxy

## Getting Started
1. Clone this repository.
2. Run the local comlink proxy by following [COMLINK_SETUP.md](docs/COMLINK_SETUP.md).
3. Restore and build the solution:

   ```bash
   dotnet restore
   dotnet build swgoh-command-bridge.sln
   ```

4. Run the Avalonia UI project:

   ```bash
   dotnet run --project src/swgoh-command-bridge.UI/swgoh-command-bridge.UI.csproj
   ```

## Documentation
- [Project specification](docs/SPEC.md)
- [Architecture overview](docs/ARCHITECTURE.md)
- [Comlink setup](docs/COMLINK_SETUP.md)
- [Mod mechanics](docs/MOD_MECHANICS.md)
- [State flow](docs/STATE_FLOW.md)
- [File manifest](docs/FILE_MANIFEST.md)
