# FileMind UI Mockup

Static, offline, dependency-free HTML/CSS/JS preview of the FileMind app. **Open `index.html` (Windows) or `android.html` (Android) directly in a browser** — no server, no build step, no network. Both run from `file://` on any machine.

## What it is

- The **canonical visual reference** of the approved UI (`docs/packs/ui-ux-foundation/`) until a real WinUI/Android project ships. Binding rule: `docs/rules/MOCKUP_RULES.md`.
- Two platform shells over one engine:
  - `index.html` — Windows desktop shell (Fluent Minimal Utility): titlebar, sidebar nav, desktop density.
  - `android.html` — Android phone shell (Material 3 Minimal Utility, PLAN-021 presentation): bottom navigation, 48dp rows, modal bottom sheets, contextual selection bar, SAF-style folder grants, permission explanation before access.
- An **interactive demo of how the codebase works**: mock data mirrors the real domain contracts, and the mock engine (`FM.engine` in `app.js`) mirrors the real pipeline — `FileScanner → ClassifierEngine → RuleEngine → DecisionMaker → ActionPlanner → SafetyEngine → TransactionEngine → UndoService` — including the same scores, caps, bands, decision ordering, templates, and `KeepBoth` conflict handling. Both shells drive the same engine and store.
- A **console simulator** replicating `src/FileMind.Windows.Host/Program.cs` verbs and output formats against the same dataset (Windows shell only).
- An **architecture view** of the verified modules and their `EXISTING`/`INCOMPLETE`/`MISSING` status per `docs/features/FEATURES.md` (Windows shell only).

Nothing here touches real files. Every "operation" mutates in-memory mock state only.

## Files

| File | Responsibility |
|---|---|
| `index.html` | Windows app shell: titlebar, command box, sidebar nav, content region, dialog/toast roots |
| `styles.css` | Windows layout + components per the UI/UX foundation pack; extended `--fm-*` vars derived from `branding/colors.json` |
| `app.js` | Icons, theme (System/Light/Dark), hash router, runtime store, mock engine (`FM.engine`), shared UI helpers — used by both shells |
| `mock-data.js` | Mock dataset mirroring `src/FileMind.Domain/Contracts/*` field-for-field — shared |
| `screens.js` | The 7 approved Windows screens: Dashboard, Files, Tools, Search, Devices, Activity, Settings |
| `flows.js` | Windows Organizer operation flow, Console simulator, Architecture screen |
| `android.html` | Android phone frame: status/system bars, M3 top app bar, selection bar, bottom nav, dialog/toast roots |
| `android.css` | M3 Minimal Utility styling on the same `--fm-*` tokens: sheets, FAB, bottom nav, 48dp rows, chips, stepper |
| `android-screens.js` | Android screens on `FM.screen`: Home, Files (long-press selection), Search, Organizer (permission → scope → plan → run), Activity, Devices, Tools, Settings, Help |

## Token contract

`index.html` links `../branding/colors.css` directly — palette can never drift. CSS variables the spec needs but `colors.css` doesn't declare are defined at the top of `styles.css` with values copied verbatim from `branding/colors.json` and marked `sync:`.

## Contract mirror

| Mock entity | Mirrors |
|---|---|
| `fileCandidates[]` | `Contracts/FileCandidate.cs` + `PermissionCapabilities` |
| `rules[]` | `Contracts/Rule.cs` (`schemaVersion: 1`) |
| `collections[]` | `CollectionEntry` (`seed-demo` values) |
| `subjects[]`/`projects[]` | `SubjectEntry`/`ProjectEntry` (`seed-demo` values) |
| `operations[]` | `Contracts/OperationRecord.cs` / `OperationItemState` |
| `smartInbox[]` | Smart Inbox repository entries |
| `enums`, `kindMap`, `mimeMap` | `Contracts/Enums.cs`, `ClassifierEngine.FileKindMap`, `Scanning/MimeMap.cs` |

## Sync rule (summary)

Any change to contracts/enums, pipeline behavior, host verbs/output, feature statuses, brand tokens, or ui-ux-foundation intent must update this mockup in the same change. Unimplemented features are shown disabled with a truthful `Planned`/`Missing` label — never fake-functional. See `docs/rules/MOCKUP_RULES.md`.

## Status legend used in the UI

- Interactive = behavior is `EXISTING` in the backend (mirrored in JS).
- Disabled + `Planned` badge = approved intent exists but the feature is `MISSING` (per FEATURES.md).
