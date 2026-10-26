# QA Rules

- QA follows implementation and precedes completion.
- Validate acceptance criteria and regressions relevant to the approved scope.
- Never report tests/checks as passed unless actually executed or verified.
- Record blocked/unavailable checks honestly.
- Repair in-scope QA findings and re-run affected checks.

## Organization-safety QA areas (per product intent)

When a change touches the organizer pipeline, QA should cover:

- **Correct classification** — kind/subject/project evidence on fixture
  cases (`tests/fixtures/manifest.json` parity on both platforms).
- **Correct destination** — template resolution incl. `{Subject}`,
  `{FileKind}`, `{RelativeDir}`, collection templates, naming templates.
- **Folder creation** — SAF grant-anchored directory creation on Android;
  never outside granted scope.
- **Filename conflicts** — KeepBoth/Skip/Ask honored; never overwritten.
- **Inaccessible/unsupported files** — permission denials, locked files,
  unknown kinds → honest failures, not silent skips or crashes.
- **Interrupted operations** — cancel/crash reconcile to `NeedsAttention`;
  committed items remain undoable.
- **Permissions** — SAF grant revocation mid-flow; protected-path denial.
- **Offline operation** — core workflow with no connectivity (AI absent).
- **Original data preservation** — sources intact until each move
  verifies; fingerprints checked before reverse moves.
- **No re-organization loops** — the `organized/` output subtree must not
  be re-scanned into itself.
- **Undo** — reverse plan restores committed items, refuses blind
  overwrite on changed destinations.
