# PLAN-008 — FileMind Duplicates & Storage Megaplan (F-P6 — fills PLAN-001 milestone gap)

Status: APPROVED
Related Feature: F-P6 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Implement F-P6 per `docs/packs/foundation-pack/02-features/FEATURES_BY_PHASE.md` P6 (zip `01_FileMind_Foundation_Pack.zip`): size prefilter, partial/full hashing, duplicate groups, similar/version candidates, large/old file analysis, storage breakdown and savings estimates.

## Repository Audit

### Verified Existing State

- `IHashService` port `EXISTING` (`src/FileMind.Domain/Ports/IFileSystemAdapter.cs`); `WindowsHashService` impl `EXISTING` (`src/FileMind.Platform.Windows/`); `HashState` enum `EXISTING` in `Enums.cs`.
- Fixture corpus includes `tests/fixtures/corpus/duplicates/` (report_a/report_b_copy/unique) `EXISTING`.
- File index repository `IFileIndexRepository` `EXISTING`; hashing/indexing not wired into pipeline (per FEATURES.md F-P1 note).
- PLAN-001's milestone sequence (M4–M10) contains no duplicates/storage phase — this plan closes that coverage gap explicitly.
- `MISSING`: duplicate-detection use case, similarity/version detection, storage analysis, dedupe plan generation, savings estimates, host/UI surface.

### Evidence / Relevant Files

- Spec: `docs/packs/foundation-pack/02-features/FEATURES_BY_PHASE.md` P6; safety rules (`protected paths`, never silently overwrite, review-by-default).
- `src/FileMind.Application/Ports/Repositories.cs` (`IFileIndexRepository`), `WindowsHashService.cs`, `Enums.cs` (`HashState`).

### Documentation vs Code Differences

Foundation pack assigns P6 a phase; backend roadmap lacks a matching phase dir — `INCONSISTENT` across packs, resolved by this plan (P6 implemented against foundation-pack intent + existing ports).

## Findings

- Spec + hash port/impl + fixtures: `EXISTING`.
- Detection/analysis/use-cases: `MISSING`.
- Roadmap-phase coverage for F-P6: was `MISSING` → resolved by this file.

## Global Constraints (from packs — verbatim intent)

Duplicates are advisory: the organizer never auto-deletes; duplicate actions produce review-first `ActionPlan`s (move-to-collection/dedupe-quarantine style destinations, never overwrite). Hashing is incremental — size prefilter → partial hash → full hash only on candidates. Full-content hashing bounded/cancellable; index updates resilient to moved/deleted files. No `delete` op in core.

## Proposed Changes / Implementation Phases

### Milestone 0 — Contracts + Index Wiring

Exit gate: `DuplicateGroup`, `SimilarityCandidate`, `StorageReport` contracts defined; file index records size + partial/full hash + `HashState`.
- 0.1 Contract records + enums in Domain; extend `file_index` schema if needed (migration, versioned).
- 0.2 Wire hashing into index pipeline (size always; partial hash on demand; full hash gated by candidate filter).

### Milestone 1 — Duplicate Detection

Exit gate: fixture duplicates grouped correctly; unique files never grouped; deterministic ordering.
- 1.1 Size prefilter → partial-hash pass → full-hash confirm → `DuplicateGroup[]` (exact dupes).
- 1.2 Similar/version candidates (name-similarity + size proximity + type match) with confidence evidence; ambiguity → review.
- 1.3 Cancellation + incremental indexing (moved/deleted invalidation).

### Milestone 2 — Storage Analysis

Exit gate: storage breakdown per scope/collection/type + large/old lists + savings estimate.
- 2.1 Aggregations over file index: breakdown by folder/collection/category/type/age bands.
- 2.2 Large/old-file analyzers (thresholds configurable); savings estimate = sum of redundant duplicate bytes.
- 2.3 `AnalyzeStorage`/`FindDuplicates` use cases + host verbs (`dupes`, `storage`) outputting plan-shaped previews.

### Milestone 3 — Safety + E2E

Exit gate: dedupe suggestions produce preview/dry-run plans; protected paths excluded; undo works.
- 3.1 Suggested actions → `ActionPlan` via SafetyEngine (Keep Both conflict policy; no deletes).
- 3.2 Fixture E2E: duplicates detected → preview → execute move-to-review-folder → verify → history → undo.
- 3.3 Docs: F-P6 status, MODULES/DATA_FLOW updates, CHANGELOG.

## Non-Goals

Auto-delete, fuzzy image/video similarity beyond declared heuristics, semantic dedupe (PLAN-013), UI surfaces (PLAN-014).

## Files Expected to Change

`src/FileMind.Domain/**` (contracts), `src/FileMind.Application/**` (use cases, index wiring), `src/FileMind.Infrastructure.Sqlite/**` (schema + queries), `src/FileMind.Platform.Windows/**` (hash usage), `src/FileMind.Windows.Host/Program.cs`, docs.

## New Files Required

Contracts + use cases + migration `NNNN_dupe_index.sql` (if schema needed) + test suites.

## Security / Privacy Impact

Hash values + paths stay local; no content leaves the machine; logs redacted.

## Error Handling / Edge Cases

Files changing between hash passes (re-stat + HashState transitions), locked/unreadable files → typed skip with reason, huge-file hashing budgets, index drift on moved/deleted files → reconcile.

## QA Plan

Unit tests (prefilter/hash stages/grouping/similarity), migration tests, fixture E2E, undo coverage (QA-matrix rule), cancellation mid-hash.

## Acceptance Criteria

- [ ] Duplicate groups detected on fixtures via size→partial→full pipeline
- [ ] Similar/version candidates flagged with confidence evidence
- [ ] Storage breakdown + savings estimate produced per scope
- [ ] `dupes`/`storage` host verbs output truthful previews
- [ ] Suggested actions flow through SafetyEngine; no silent mutation
- [ ] F-P6 status updated after verification

## Risks / Regression Areas

Hash cost on large trees → budgets + incremental; false-positive similarity → review-first; index staleness → HashState + reconcile.

## Documentation Updates

`FEATURES.md` F-P6, `MODULES.md`, `DATA_FLOW.md`, `CHANGELOG.md`.

## Commit Plan

`feat(domain|application): dupe/storage contracts + index wiring` → `feat(application): detection + analysis use cases` → `feat(host): dupes/storage verbs` → `test(*):` → `docs(*):`.

## Approval

User approval required before implementation.
