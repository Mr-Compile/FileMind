# PLAN-015 — FileMind Search Megaplan (Backend Roadmap Phase 8 / PLAN-001 M9)

Status: READY
Related Feature: F-P9 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Implement `docs/packs/backend-roadmap/08-search/PLAN.md` (zip `02_FileMind_Complete_Backend_Roadmap.zip`, outline in PLAN-001 M9): local search over the file index — filename, collection, subject, project, category, type, date, metadata, and full-text filters via SQLite FTS5; asynchronous, cancellable, incremental indexing resilient to moved/deleted files; NL query interpretation optional via Groq (PLAN-009); semantic/vector indexing deferred (PLAN-013). Numbering note: PLAN-011 was allocated to the logo-asset plan by a concurrent session; this plan takes PLAN-015.

## Repository Audit

### Verified Existing State

- `ISearchIndex` port `EXISTING` (`src/FileMind.Application/Ports/AppPorts.cs:54`); `IFileIndexRepository` `EXISTING`.
- SQLite schema `EXISTING` (`FileMindDb.cs`): `file_index(path, stable_id, fingerprint, scan_id, payload_json, scanned_at)` + fingerprint index — plain table, **no FTS5 virtual table present** (verified by grep 2026-10-24).
- PLAN-001 M3.3 claimed "`file_content_index` FTS5 prepared" — `INCONSISTENT` with code; this plan delivers the actual FTS5 work via migration.
- `MISSING`: FTS5 virtual table + population, indexing pipeline, search query model, filters, SearchUseCase, host verb, NL interpretation.

### Evidence / Relevant Files

- Spec: `08-search/PLAN.md`; `src/FileMind.Infrastructure.Sqlite/FileMindDb.cs` (lines 104–107 file_index), `SqliteStore.cs`; `tests/FileMind.Application.Tests/` (E2E patterns).

### Documentation vs Code Differences

- FTS5 "prepared" claim in PLAN-001 vs absent table — `INCONSISTENT`, resolved here.

## Findings

- Search port + file_index base table: `EXISTING`.
- FTS5 + indexing + search surface: `MISSING`.
- PLAN-001 doc claim: `INCONSISTENT` (recorded; corrected docs shipped with implementation).

## Global Constraints

Indexing async/cancellable/incremental; resilient to moved/deleted files (reconcile via stable_id/fingerprint); FTS content may include extracted text — privacy: local-only, never leaves DB; NL interpretation may use Groq but retrieval stays local; no provider dependency in retrieval path.

## Proposed Changes / Implementation Phases

### Milestone 0 — Schema + Index Model

Exit gate: migration adds `file_content_index` FTS5 virtual table + mapping to `file_index`; migration tests green.
- 0.1 Migration `NNNN_search_fts.sql`: FTS5 table (content= external or standalone — decided in implementation audit), tokenization choice documented.
- 0.2 `SearchIndex` impl of `ISearchIndex`: add/update/remove; staleness detection.

### Milestone 1 — Indexing Pipeline

Exit gate: indexing runs async, cancellable, incremental; restart-safe.
- 1.1 `IndexingService`: feeds scanner/enrichment output → index rows; incremental diff via fingerprint/scan_id; moved/deleted reconciliation.
- 1.2 Cancellation + progress + bounded batches; integrates `IJobQueue` when PLAN-005 lands (graceful without it — direct runner).

### Milestone 2 — Search Query + Use Case

Exit gate: filtered queries return correct results on fixture corpus.
- 2.1 Query model: text + filters (name/collection/subject/project/category/type/date/metadata); safe FTS query construction (escaping, column filters).
- 2.2 `SearchUseCase` + repository queries; ranking via FTS bm25 defaults documented.
- 2.3 Host `search` verb; truthful empty-result states.

### Milestone 3 — NL Interpretation Hook + Docs

- 3.1 Optional `IAIProvider` query-interpretation → structured filters (advisory; degrades to plain FTS when AI off/unavailable).
- 3.2 F-P9 status, DATA_FLOW/MODULES docs, CHANGELOG.

## Non-Goals

Semantic/vector index (PLAN-013), content indexing of non-extracted binaries beyond declared extractors (PLAN-007 dependency recorded), UI surfaces (PLAN-014).

## Files Expected to Change

`src/FileMind.Infrastructure.Sqlite/**` (migration + impls), `src/FileMind.Application/**` (indexing + search use cases), `src/FileMind.Windows.Host/Program.cs`, docs.

## New Files Required

`NNNN_search_fts.sql` migration, `SearchIndex`/`IndexingService`/`SearchUseCase` impls, test suites.

## Security / Privacy Impact

Indexed content stays local in SQLite; redacted logs; AI interpretation opt-in only.

## Error Handling / Edge Cases

Index corruption → rebuild path; files moved mid-index → reconcile; FTS syntax errors from user input → safe escaping + friendly error; DB locked during batch index → retry policy.

## QA Plan

Migration tests; indexing unit/integration tests (incremental, restart, moved/deleted); query correctness on fixtures; cancellation tests; perf sanity on 10k corpus (ties to PLAN-006 budgets).

## Acceptance Criteria

- [ ] FTS5 migration applied; `file_content_index` populated incrementally
- [ ] Filters work: name/collection/subject/project/category/type/date/metadata/FTS
- [ ] Indexing cancellable + restart-safe; moved/deleted reconciled
- [ ] `search` host verb returns truthful results incl. empty states
- [ ] NL interpretation optional; local retrieval works fully offline
- [ ] F-P9 status updated after verification

## Risks / Regression Areas

FTS index drift → reconcile + rebuild; query-injection via FTS syntax → parameterized/escaped builders; large-corpus index time → batch budgets.

## Documentation Updates

`FEATURES.md` F-P9, `DATA_FLOW.md` index stage, `MODULES.md`, `CHANGELOG.md`; PLAN-001 FTS5 claim corrected in docs.

## Commit Plan

`feat(infrastructure-sqlite): FTS5 migration + search index` → `feat(application): indexing service + search use case` → `feat(host): search verb` → `feat(intelligence-ai): NL query hook` → `test(*):` → `docs(*):`.

## Approval

User approval required before implementation.
