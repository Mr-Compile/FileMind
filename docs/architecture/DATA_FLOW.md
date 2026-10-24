# Data Flow

Document verified input → processing → persistence/output flows, including failure paths.

## Duplicates & storage (verified 2026-10-24)

1. `scan` → `FileIndexingService` upserts `file_index` rows (`payload_json` = `FileCandidate`, `content_hash`, `hash_state`).
2. `dupes` → `DuplicateDetectionService` reads `IFileIndexRepository.GetAllAsync` → size prefilter → partial hash → full hash (reuses index hashes when deep enough; re-stats before hashing; size drift removes the stale row). Over-`maxFullHashBytes` groups stay `Confirmed=false`.
3. `dupes --plan` → `DuplicatePlanBuilder` emits Move items into `duplicates-review` with `KeepBoth` + `UndoRecipe`s, confirmed groups only → `SafetyEngine.ValidateAsync` via `pipeline.DryRunAsync` → preview. `--apply` → `TransactionEngine.ExecuteAsync` → verified + persisted operation → `undo` reverses through `UndoService` recipes.
4. `storage` → `StorageAnalysisService` aggregates the same index (kind, age bands, largest/oldest, reclaimable = redundant duplicate bytes). Read-only; advisory.

No flow deletes files. Partial-only groups are never planned for action.
