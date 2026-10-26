# PLAN-030 — Device-run repairs + real folder taxonomy

Status: APPROVED (user instruction after Infinix run: "the move is working but
the activity shows needs attentions", "it should just make a one subfolder name
like course subject", "do not make a folder name organized", "update the rules
and the codebase and audit it").

## Findings from the device run

1. `moveSaf` returns the *predicted* `destination` string even when
   `DocumentsContract.moveDocument` returns a different real doc URI
   (`AndroidFileSystemAdapter` ~line 383). `resultingLocation` then points at a
   URI that may not resolve → post-move verify fails → every item
   `NeedsAttention` despite a successful move. Undo also uses the wrong URI.
2. SAF fingerprint verify compares `size:mtime` — providers may rewrite mtime
   on move → false mismatch even when the URI is right.
3. Ops left in `Planned`/`Executing` when the process dies stay "Running now"
   forever — no startup reconciliation.
4. Destination root is hardcoded `<source>/organized` — user wants the
   structure inside the selected folder directly.
5. Generic `Files/{FileKind}` + subject `School/{Subject}/{FileKind}` don't
   match the intended taxonomy: `School/IT-421/{Assignments|Activities|Labs|
   Notes|Projects}` and type folders `Documents/Images/Videos/Music/Archives/
   Other`.
6. Re-running on the same folder risks "(2)" duplicates / nested
   `organized/organized` — no idempotency guard.

## Changes

- `moveSaf` records the real resulting URI; Android `verify` tolerates mtime
  drift on SAF (size component must match).
- Destination root = selected source root (in-place organize). Planner skips
  items whose resolved destination equals the source path (idempotent re-runs).
  Windows console `organize` keeps its explicit `--dest`.
- New template tokens: `{KindFolder}` = plural map
  (Document→Documents, Image→Images, Video→Videos, Audio→Music,
  Archive→Archives, else→Other); `{SubKind}` = school-activity bucket from
  filename keywords (assign→Assignments, activity/quiz/exam→Activities,
  lab→Labs, note/reviewer/module/syllabus/handout→Notes,
  project/capstone/thesis→Projects) else `{KindFolder}`.
- Default templates: subject → `School/{Subject}/{SubKind}`;
  project → `Work/{Project}/{KindFolder}`; generic → `{KindFolder}`.
- Startup reconciliation: ops in non-terminal statuses with no live run →
  `NeedsAttention` ("interrupted — tap to review/undo").
- Parity: Android VM + WinUI page + mockup + tests.

## Non-goals

No rename/naming-template changes; no Smart Inbox redesign; F-B3 nav-sheet fix
stays deferred.
