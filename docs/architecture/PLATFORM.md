# Platform Architecture

Document only verified platform targets, shared abstractions, platform-specific implementations, permissions, packaging, and constraints.

## Targets

| Target | Stack | Storage access | Persistence | Background work | Verified |
|---|---|---|---|---|---|
| Windows | .NET 10, `FileMind.WinUI` desktop app (WinUI 3 / Windows App SDK 1.8) + `FileMind.Windows.Host` console + `FileMind.Platform.Windows` adapters | NTFS paths, `FileSystemWatcher`, DriveInfo | `SqliteStore` (schema v4) | in-process watch loop + `SqliteJobQueue` | 2026-10-24 |
| Android | Kotlin, `android/core` (pure JVM) + `android/platform` (AGP 9.0.1 library, minSdk 26, compileSdk 36) | SAF persisted tree grants (`takePersistableUriPermission`), MediaStore rows/RELATIVE_PATH, app-private `file://` only — no unrestricted path access | Room `FileMindDatabase` (schema v2, `MIGRATION_1_2` adds `ai_usage`) | WorkManager `ScanWorker`/`OrganizeWorker`, cooperative cancel | compile + JVM tests 2026-10-24; instrumented UNVERIFIED (no device) |

## Shared abstractions (identical semantics both platforms)

`FileSystemAdapter`, `PathNormalizer`, `ProtectedPathProvider`, `OperationStore`,
repository ports (rules/collections/subjects/projects/file-index/operations/
smart-inbox/exclusions/protected-paths/watched-folders/settings/jobs/ai-*),
domain engine (normalize → classify → rules → decide → plan → safety →
transaction → history/undo), and the shared fixture
`tests/fixtures/manifest.json` consumed by `ManifestParityTests` (xUnit) and
`ManifestParityTest` (JUnit) — including the golden fingerprint
`400:8de541cd86c9000` proving `UtcTicks`/`utcTicks` parity.

## Android capability model

"Path" on Android is a URI; `AndroidUris` classifies `SafTree`/`SafDocument`/
`MediaStoreRow`/`MediaStoreFolder`/`AppFile`/`Other`, and
`AndroidCapabilityModel` maps each to `PermissionCapabilities`:

- SAF under a persisted grant: full read/write/move/rename/create-folder —
  but only same-provider moves (`canMoveBetween` refuses cross-provider and
  SAF↔MediaStore); MediaStore destinations have no explicit mkdir
  (`canCreateFolder=false`, RELATIVE_PATH implies folders).
- Foreign `content://` providers: read-only at best, never treated as paths.
- `GrantCoverage.isCovered` + `UriGrantStore.revalidate` detect revoked and
  partially-held grants before every scan/organize run.
- `AndroidProtectedPaths` denies `Android/data`, `Android/obb`,
  `Android/sandbox`, `.android_secure` on every volume plus user prefixes.

## Android mutation guarantees

Same rules as Windows: every mutation originates from an `ActionPlan`, passes
`SafetyEngine` (protected paths, capability overrides, destination-conflict
policy), commits through `TransactionEngine` with persisted `OperationRecord`
transitions, and verifies via fingerprint. `KeepBoth` uses deterministic
` (n)` suffixes; `Ask`/`ReplaceWithConfirmation` never overwrites silently.
Undo is available only for operations with recipes on providers that support
reversal (SAF `moveDocument`, MediaStore RELATIVE_PATH); everything else
surfaces as `NeedsAttention`/review.

## Constraints

- No deletes on either platform (undo of Copy ⇒ review, not delete).
- Share-target intake is headless (`ShareIntakeActivity`, translucent
  `noHistory`) — persists read grants best-effort, routes to Smart Inbox.
- POST_NOTIFICATIONS runtime permission (API 33+) is surfaced via
  `NotificationManagerCompat` failures, not swallowed.
- Android UI is out of scope (PLAN-010 non-goal); the platform module is a
  consumable backend library.
