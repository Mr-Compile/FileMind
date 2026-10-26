# Architecture Rules

- Audit the existing architecture before proposing changes.
- Preserve established boundaries unless an approved plan justifies change.
- Do not create duplicate services, repositories, models, routing systems, state systems, configuration systems, or abstractions without verified need.
- New architectural rules must be explicitly approved; never infer them.

## Verified boundary rules

- **Domain/core contains no platform filesystem code** — no `System.IO`,
  SAF, MediaStore, WinUI, or Android imports; all I/O goes through ports
  (`IFileSystemAdapter`, `PathNormalizer`, repositories).
- **UI never mutates files** — mutations flow through `OrganizerPipeline`
  (or the Android VM calling it) only.
- **AI never mutates files** — advisory evidence merges into
  classification; the deterministic pipeline executes.
- **Contracts are shared, semantics mirrored** — Kotlin `android/core`
  ports mirror the C# domain; cross-platform behavior is proven by the
  shared fixture `tests/fixtures/manifest.json` (ManifestParity tests on
  both runtimes).
- **Per-item transactional persistence** — operation records persist
  state per item so crashes reconcile honestly; recovery runs at startup.
- **Result/error types, not exceptions, at boundaries** — typed
  `Result`/`FileMindError`; exceptions may only exist inside adapters and
  must map to typed errors at the port edge.
