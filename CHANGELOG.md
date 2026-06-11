# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial project structure with `Core`, `UI`, and `Tests` projects.
- Basic Avalonia MVVM setup in the `UI` project.
- Core data models for `GameMod`, `Character`, `ModStat`, and enums.
- Initial project documentation (`README`, `ARCHITECTURE`, `SPEC`, `TODO`, `CODING_STANDARDS`).
- Character, mod inventory, priority, threshold, and optimizer view/view-model scaffolds.
- Early mod advisor, player service, and mod assignment service scaffolds in the compiled Core project.
- Root-level EF Core entity, repository, Comlink, `swgoh.gg` scraper, and mod assignment integration drafts.
- Local SQLite package reference for the Core project.
- Compiled Core `SwgohGgScraperService` and interface for scraping best-mod pages, retrying transient failures, and persisting set/primary recommendation JSON.
- SQLite-backed UI viewmodel behavior for character lists, character priorities, mod filtering/sorting, selected-mod recommendations, and optimizer community recommendation display.

### Changed
- **Project Scope:** The primary goal has been refocused to be a **read-only** analysis and recommendation tool. Features related to direct account modification (e.g., equipping or upgrading mods) have been moved to stretch goals.
- Documentation refreshed to reflect the current mixed state of compiled project scaffolding, root-level persistence drafts, and the Core `swgoh.gg` scraper.
