# Product Scope

## In Scope

- Scanning files recursively on local storage (SAF grants, MediaStore,
  app-private on Android; NTFS paths on Windows).
- Understanding files: extension/MIME kind, filename tokens, metadata and
  extracted content where analyzers exist.
- Classification: kind + subject + project + confidence with explainable
  evidence.
- Decisions via rules, collections, confidence bands → Organize / Review /
  Ignore / NoOp; low confidence routes to Smart Inbox.
- Creating folder structures under a per-source `organized/` root from
  destination templates (`School/{Subject}/{FileKind}`, `Files/{FileKind}`,
  user-defined).
- Safe file mutations through the ActionPlan → Safety → Transaction path:
  move/copy/rename, conflict policies, per-item verify, history, undo
  (including partially-completed runs).
- User control surfaces: preview, dry-run, modes (Manual/Smart/Automatic),
  rules, subjects/projects/collections management, exclusions, Smart
  Inbox resolution.
- Advisory analysis: duplicates detection, storage breakdown, related/
  version chains, correction learning (draft rules from undos).
- Optional AI advice (Groq): classification assist, rule drafting, NL
  search — advisory only, gated, offline-fallback.
- Profile import/export/backup/restore of portable config.
- HTML mockup maintained in parity with shipped UI semantics.

## Out of Scope

- Cloud sync or backup of user files.
- Destructive cleanup/delete inside the core organizer.
- AI-driven filesystem mutation (AI never executes).
- General-purpose file management (view/edit/share/print).
- Social/collaboration features, accounts, system cleaning.
- Mandatory connectivity for any core workflow.

## Platform Scope

- **Windows**: full NTFS path access, FileSystemWatcher, background
  watch loop, console host + WinUI 3 app. EXISTING.
- **Android**: SAF persisted tree grants + MediaStore + app-private only —
  no unrestricted path access by platform design; Compose app +
  WorkManager. EXISTING (on-device verified).
- Capability differences are intentional; see
  `docs/architecture/PLATFORM.md`.
