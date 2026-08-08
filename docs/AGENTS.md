# Agents

## Background Agents & Services
This section describes any automated agents, background services, or long-running processes that are part of the application ecosystem.

*There are currently no scheduled long-running in-app agents defined for this project.*

`SwgohGgScraperService` is a Core service that can run a user-triggered, incremental, sequential scrape of cached roster characters. It reports `ScrapeProgress`, supports cooperative cancellation, skips fresh cached recommendations, records per-character failures, and uses an explicit `ScrapeRetryPolicy` for bounded attempts, exponential backoff, server retry timing, and inter-request pacing. It is not hosted as a scheduled background worker; this file should be updated if a background sync, periodic cache refresh, or automation host is introduced.

On Windows x64, `ComlinkRuntimeManager` may own a hidden child process for the duration of the desktop session. This is a lifecycle-managed local dependency, not an autonomous agent: it starts only when the configured local endpoint is unavailable and is stopped during composition disposal. Linux and macOS currently require an externally managed Comlink service.

The desktop composition root owns the long-lived database and HTTP client. Do not introduce a hosted worker or automatic account sync without updating the read-only scope, privacy documentation, and smoke-test checklist.

---

## AI Coding Agent & Context Optimization Guidelines
To ensure that modern AI coding assistants (such as LLM-based agents) can work effectively within this codebase, we enforce rules to minimize context usage, token overhead, and cognitive load:

1. **Documentation Efficiency**: Project documentation, guides, and architectural notes must be highly structured, concise, and optimized to prevent context bloat. Avoid overly wordy descriptions; prefer structured lists, clean diagrams, and precise definitions.
2. **Context-Conscious File Sizing**:
   - **Soft Rule**: Keep code files compact. Ideally, source files should remain below **300 lines of code (LOC)**.
   - **Modular Architecture**: Rather than expanding existing monolithic files, proactively split components, services, and view models into smaller, dedicated files and sub-components.
   - **Benefits**: This prevents AI agents from having to ingest irrelevant portions of a large file, reducing inference latency, minimizing token consumption, and enhancing the precision of generated code completions and bug fixes.
3. **Coding Standards Adherence**: All code suggested, refactored, or edited by AI coding agents must strictly conform to the guidelines specified in [CODING_STANDARDS.md](../CODING_STANDARDS.md) (including MVVM separation, asynchronous method patterns, Allman-style braces, and naming conventions).
