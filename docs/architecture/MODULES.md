# Modules

Inventory verified modules/components, their responsibilities, dependencies, and ownership boundaries. Do not create an inventory from assumptions.

| Module | Responsibility | Dependencies | Verified |
|---|---|---|---|
| `FileMind.Branding` | Platform-neutral brand color tokens (`BrandTokens`) + `ThemeMode`/`ThemePalette` resolution. Mirrors `branding/colors.json`; parity enforced by `FileMind.Branding.Tests`. | None (net10.0, no packages, no OS/UI APIs) | 2026-10-24 |
| `mockup/` | Dependency-free static HTML/CSS/JS interactive mockup of the approved UI — spec screens, organizer pipeline demo, console host simulator, architecture view. Links `branding/colors.css` directly so tokens can't drift. Sync rule: `docs/rules/MOCKUP_RULES.md`. | None (browser only; links `branding/colors.css`) | 2026-10-24 |
| `FileMind.Application/Duplicates` | `DuplicateDetectionService` (size→partial→full hash gating over `IFileIndexRepository`, stale-row invalidation, advisory similarity candidates) + `StorageAnalysisService` (kind/age buckets, largest/oldest, reclaimable estimate) + `DuplicatePlanBuilder` (review-first quarantine `ActionPlan`, KeepBoth, undo recipes). No delete path. | `IFileIndexRepository`, `IHashService`, `IFileSystemAdapter`, Domain contracts | 2026-10-24 |
| `FileMind.Application/Search` | `SearchUseCase` — FTS5 full-text (`ISearchIndex`) + structured filters (name/ext/kind/age) over `IFileIndexRepository`. Local-only retrieval. | `ISearchIndex`, `IFileIndexRepository` | 2026-10-24 |
| `file_content_index` (schema v3) | FTS5 virtual table (`unicode61`; `path`, `display_name`, `collection`, `subject`, `project`, `kind`, `content`). Populated by `FileIndexingService` when an `ISearchIndex` is wired; row deleted when the `file_index` row is removed. `ISearchIndex` impl lives on `SqliteStore`. | `e_sqlite3` FTS5 | 2026-10-24 |
