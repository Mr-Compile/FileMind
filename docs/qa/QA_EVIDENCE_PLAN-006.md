# PLAN-006 QA Evidence — Release Gate

Date: 2026-10-24. Runner: `dotnet fsi tools/sac-test-runner.fsx -- <bin dirs>`
(artifacts under `C:\tmp\fm-artifacts` — SAC blocks unsigned DLLs under the
WAMP docroot). Suite result: **294 pass / 0 fail** (Domain 90, Application 196,
Branding 8).

## Gate statement

No known silent data-loss path. Every injected failure surfaces as a typed
`FileMindError`, a safety blocker, or `NeedsAttention`/`NeedsReview`. Every
mutating operation the pipeline emits (`Move`) carries a fingerprint-checked
`UndoRecipe`.

## Milestone evidence

| Milestone | Exit gate | Status |
|---|---|---|
| M0 gap table | every QA_MATRIX row labeled + evidence | DONE — table in PLAN-006 findings |
| M1 adapter contract | Fake + Windows pass shared suite | DONE — `AdapterContractTests` (50 runs, 25 assertions × 2 adapters) |
| M2 stress | 10k/50k within budgets, bursts handled | DONE — `StressTests` (6 tests) |
| M3 failure injection | typed error or NeedsReview, never silent | DONE — `FailureInjectionTests` (11 tests) |
| M4 release gate | undo coverage audited, evidence recorded | DONE — this document |

## Findings fixed during this plan

1. `FakeFileSystemAdapter.Ask` conflict silently overwrote → typed
   `DestinationConflict` (commit `142b78e`).
2. `FakeFileSystemAdapter.CheckCapabilities` returned Ok for missing paths →
   `SourceNotFound` (commit `142b78e`).
3. Fake path keys were separator-literal — `dir/a` and `dir\a` were distinct
   entries, hiding conflicts → canonical `Key()` at every access (commit `142b78e`).
4. `FakeFileSystemAdapter.RenameAsync` treated `newName` as a full path →
   combines with source directory like Windows (commit `142b78e`).
5. SQLite wrote with per-statement autocommit — 10k index took 2m57s →
   `PRAGMA journal_mode=WAL` + `synchronous=NORMAL` at `FileMindDb.Open`
   (commit `83759db`). WAL preserves crash durability.

## Undo coverage per OperationType

| OperationType | Mutating | Recipe source | Undo verified |
|---|---|---|---|
| Move | yes | `ActionPlanner` (all organize plans) | yes — `TransactionTests.Undo_CommittedOp_Reverses`, `WindowsEndToEndTests` + preserve-tree E2E undo |
| Rename | yes | hand-attachable `UndoRecipe` | yes — `TransactionTests.Undo_Rename_RecipeReverses` |
| Copy | yes | none emitted | **GAP** — no Delete op exists; `BuildReversePlan` fails visibly (`Copy_WithoutRecipe_Executes_UndoNeedsReview`) |
| CreateFolder | yes | none emitted | **GAP** — same; folder removal not expressible |
| Tag / Ignore / Notify / Ask | no | n/a | commit without recipes — `NonMutatingOps_Commit_RequireNoRecipe` |

## Known limitations (disclosed, not silent)

- `Copy`/`CreateFolder` undo is impossible until a Delete/Remove op exists;
  `UndoService` refuses with a typed validation error and per-item NeedsReview
  blockers — never a blind mutation.
- Windows permission-denied is exercised via `CapabilityOverrides` (deny-list
  injection), not a live ACL — a real ACL-denied path returns the same
  `StatAsync` failure → blocker path.
- `Windows.IntegrationTests` project is not run in the standard suite (SAC
  constraint); its scenarios are covered in-process by
  `WindowsEndToEndTests`/`WatchPipelineTests` on real temp FS + SQLite.

## Reproduce

```
dotnet build FileMind.slnx --artifacts-path C:/tmp/fm-artifacts
dotnet fsi tools/sac-test-runner.fsx -- \
  C:/tmp/fm-artifacts/bin/FileMind.Domain.Tests/debug/ \
  C:/tmp/fm-artifacts/bin/FileMind.Application.Tests/debug/ \
  C:/tmp/fm-artifacts/bin/FileMind.Branding.Tests/debug/
```
