# PLAN-018 — Preserve Source Folder Tree on Organize (Category-Then-Tree)

Status: APPROVED
Related Feature: F-P1 (Core Foundation — move/organize), F-P4 (watch folders — same pipeline path)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

User wants `filemind execute <Downloads> --dest <Documents>` to keep Downloads' internal subfolder structure inside each category folder instead of flattening all files into `{Category}/`.

Chosen layout (user-approved): **category then tree**.

`Downloads\receipts\jan\a.pdf` → `Documents\Files\Documents\receipts\jan\a.pdf` (category template resolves first, the source-relative directory is appended inside it).

## Repository Audit

### Verified Existing State

- `FileScanner.ScanAsync(root, recursive)` enumerates files under `root` and yields `FileCandidate`s. `FileCandidate` carries `SourceUri` (full path) but **no scan root and no relative path** — the root is dropped after enumeration. `src/FileMind.Application/Scanning/FileScanner.cs`
- `OrganizerPipeline.Plan(analyzed, dryRun, destinationRoot, now)` builds `PlanContext(Paths, DestinationBase, Now)` — no source-root or preserve flag exists. `src/FileMind.Application/UseCases/OrganizerPipeline.cs:66`
- `ActionPlanner.Plan` resolves `decision.DestinationTemplate ?? "{Collection}/{FileKind}"` via `ResolveTemplate`, applies `DestinationBase`, then `Combine(destination, newName)` into a `Move` item. `src/FileMind.Domain/Operations/ActionPlanner.cs:50-80`
- `ResolveTemplate` tokens today: `{Collection} {Category} {Subcategory} {Subject} {Project} {FileKind} {Name} {Ext} {YYYY} {MM} {DD}`. No relative-dir token. `ActionPlanner.cs:96-111`
- `IPathNormalizer` exposes `Normalize/Combine/IsUnder/FileName/DirectoryOf` — **no relative-path primitive**. `src/FileMind.Domain/Ports/IFileSystemAdapter.cs:51-58`, impl `src/FileMind.Platform.Windows/WindowsPathNormalizer.cs`
- Host verbs: `plan <dir> [--dest <root>]` and `execute <dir> [--dest <root>] [--dry-run] [--apply] [--shallow]`. `DefaultDest` = `<dir>\organized`. `src/FileMind.Windows.Host/Program.cs:29-30,405`
- Watch path: `WatchOrchestrator.ProcessEventAsync` builds a `FileCandidate` per event and calls `pipeline.Plan([file], dryRun:false, destinationRoot)`. The watched-root owner is resolved in `Program.cs` `Route()` (prefix match over `folders`) but is not exposed to the orchestrator. `src/FileMind.Application/Watching/WatchOrchestrator.cs:109-118`, `Program.cs:318-324`
- `UndoRecipe` stores the concrete `destPath`, so undo needs no awareness of how the path was produced.

### Documentation vs Code Differences

- FEATURES.md F-P1 describes "destinations" generically; no documented statement about preserving or flattening source structure. No doc/code conflict — this is new capability, not a repair.

## Findings

- Relative-path capture between scan root and file: `MISSING` (root never reaches the planner).
- `IPathNormalizer.RelativeTo` primitive: `MISSING`.
- `{RelativeDir}` template token: `MISSING` (`PROPOSED` here).
- Host flag for opt-in tree preservation: `MISSING` (`PROPOSED` here).
- Scan/plan/execute/undo machinery, safety validation, conflict policy: `EXISTING` — unchanged by this plan.

## Root Cause / Problem

Flattening is not a bug; it is an absence. The planner only knows the template tokens and the file's display name. Nothing carries "where under the scan root did this file live," so the tree cannot be reproduced under the destination.

## Proposed Changes

1. **`IPathNormalizer.RelativeTo(string path, string root) -> string?`** — returns the normalized path of `path` relative to `root` using `IsUnder` semantics (case-insensitive on Windows), or `null` when `path` is not under `root`. Implement in `WindowsPathNormalizer`.
2. **`PlanContext` gains** `string? SourceRoot = null` and `bool PreserveTree = false`.
3. **`ActionPlanner`**
   - Compute `relDir` once per decision: `SourceRoot` set → `Paths.RelativeTo(Paths.DirectoryOf(c.SourceUri) ?? "", SourceRoot)`; normalize `null`, `"."`, `""` → `""`.
   - New token `{RelativeDir}` in `ResolveTemplate` (resolves to `relDir`, empty when unknown — template authors can use it explicitly, e.g. `Work/{Project}/{RelativeDir}`).
   - When `ctx.PreserveTree` and `relDir` non-empty: `destination = Combine(destination, relDir)` — appended **after** the resolved template (category-then-tree).
4. **`OrganizerPipeline.Plan`** gains optional `sourceRoot` + `preserveTree` params, forwarded into `PlanContext`. Backward-compatible defaults keep every existing caller unchanged.
5. **Host (`Program.cs`)**
   - New flag `--preserve-tree` on `plan` and `execute`; `sourceRoot` = the `<dir>` positional arg.
   - Watch: `Route()` already resolves the owning watched folder — extend its return to also yield the owner root, pass it through `WatchService`/`ProcessEventAsync` as `sourceRoot`; gate with a per-folder setting `watch.tree.<dir>` (`filemind watch add <dir> --mode X --tree` / `watch.tree` toggle) so watch behavior is opt-in per folder.
   - Usage text + `FILEMIND_DEST` section updated.

## Non-Goals

- No change to default behavior — without `--preserve-tree` or `{RelativeDir}` in a template, output paths are byte-identical to today.
- No flattening of rename semantics, conflict policies, or undo.
- No Android implementation (interface member gets a stub/default or is implemented when F-P8 lands — decide at implementation time per platform conventions).
- No UI screen work; mockup console-simulator help text updated only if `docs/rules/MOCKUP_RULES.md` deems the flag a host-contract change (verify during implementation).

## Files Expected to Change

- `src/FileMind.Domain/Ports/IFileSystemAdapter.cs` — interface member
- `src/FileMind.Platform.Windows/WindowsPathNormalizer.cs` — impl
- `src/FileMind.Domain/Operations/ActionPlanner.cs` — `PlanContext`, `{RelativeDir}`, preserve-tree append
- `src/FileMind.Application/UseCases/OrganizerPipeline.cs` — `Plan` signature
- `src/FileMind.Application/Watching/WatchOrchestrator.cs` — sourceRoot plumbing
- `src/FileMind.Application/Watching/WatchService.cs` — pass-through (verify exact shape during implementation)
- `src/FileMind.Windows.Host/Program.cs` — flag, route tuple, usage text
- `tests/` — new unit tests (project layout per existing suites)

## New Files Required

None beyond test-file additions in existing test projects.

## Architecture Impact

Additive interface member + additive PlanContext fields + additive optional params. No breaking change to `OrganizerPipeline` callers (defaults preserve behavior).

## Data / Storage Impact

None. Operation records already persist concrete paths; `file_index` stable-id reuse on moved files unchanged.

## Routing / Flow Impact

Only destination-path computation inside `ActionPlanner`. Decision kinds, mode policies, safety validation untouched.

## Platform Impact

Windows implemented. `IPathNormalizer` gains a member every platform adapter must implement — flag for F-P8 Android port.

## Security / Privacy Impact

None. Relative path derives from already-local paths; no new I/O surface.

## Error Handling / Edge Cases

- File directly in scan root → `relDir=""` → identical to today.
- `--shallow` → all `relDir=""` → identical to today.
- `SourceUri` not under `SourceRoot` (shouldn't happen; defensive) → `null` → treated as `""`.
- Two subdirs containing same filename → now land in different tree locations (fewer conflicts than today).
- `{RelativeDir}` in a NamingTemplate resolves to `""` — document that it's a directory token only.
- Empty-path segments guarded by existing `Combine` (it drops empty parts).
- Watch event where owner root unknown → `sourceRoot=null` → `""`.

## Implementation Phases

1. `RelativeTo` on normalizer + unit tests.
2. `PlanContext`/planner token + preserve-tree append + unit tests.
3. Pipeline `Plan` params + host `--preserve-tree` on plan/execute + E2E fixture verification.
4. Watch plumbing (owner root → orchestrator, per-folder `watch.tree.<dir>` setting) + tests.
5. Docs updates (FEATURES.md F-P1 note, this plan status, usage docs if present; mockup per MOCKUP_RULES check).

## QA Plan

- Unit: `RelativeTo` (nested, root-equal, not-under, case-mixed, trailing-slash).
- Unit: `{RelativeDir}` token resolution; preserve-tree append; empty/`.` no-ops; naming template untouched.
- E2E via `tools/sac-test-runner.fsx` pattern + manual host run: temp `Downloads\{a.pdf, receipts\jan\b.pdf}` → `execute --dest Documents --preserve-tree --apply` → assert `Documents\Files\<Kind>\receipts\jan\b.pdf`; `undo` restores both.
- Regression: same fixture without the flag → flat output identical to pre-change behavior.
- Watch: file into `watched\sub\` with `watch.tree` on → lands `dest\<cat>\sub\`.

## Acceptance Criteria

- `plan`/`execute --preserve-tree` produces category-then-tree destinations; off → unchanged.
- `{RelativeDir}` usable in rule `destinationTemplate`s.
- Watch honors `watch.tree.<dir>`; unset → unchanged.
- All existing tests still green; new tests cover the above.
- Undo round-trips preserved-tree moves.

## Risks / Regression Areas

- `Plan` signature change — mitigated by optional params.
- New `IPathNormalizer` member breaks any other implementers — grep at implementation time (only Windows impl verified today).
- Rules already using a literal `{RelativeDir}` string — none verified; token list is fixed today.

## Documentation Updates

- This plan's status; `docs/features/FEATURES.md` F-P1 line; host usage text; mockup console help iff MOCKUP_RULES requires.

## Commit Plan

- `feat(domain): add RelativeTo path primitive and RelativeDir/preserve-tree planning` (phases 1–2)
- `feat(host): --preserve-tree flag on plan/execute` (phase 3)
- `feat(watch): per-folder tree preservation for watched folders` (phase 4)
- `docs(plans): PLAN-018 status + FEATURES.md` (phase 5)

## Approval

User approval required before implementation.
