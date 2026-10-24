# FileMind UI Mockup

Static, offline, dependency-free HTML/CSS/JS preview of the FileMind app. **Open `index.html` directly in a browser** — no server, no build step, no network. It runs from `file://` on any machine.

## What it is

- The **canonical visual reference** of the approved UI (`docs/packs/ui-ux-foundation/`) until a real WinUI/Android project ships. Binding rule: `docs/rules/MOCKUP_RULES.md`.
- An **interactive demo of how the codebase works**: mock data mirrors the real domain contracts, and the mock engine (`FM.engine` in `app.js`) mirrors the real pipeline — `FileScanner → ClassifierEngine → RuleEngine → DecisionMaker → ActionPlanner → SafetyEngine → TransactionEngine → UndoService` — including the same scores, caps, bands, decision ordering, templates, and `KeepBoth` conflict handling.
- A **console simulator** replicating `src/FileMind.Windows.Host/Program.cs` verbs and output formats against the same dataset.
- An **architecture view** of the verified modules and their `EXISTING`/`INCOMPLETE`/`MISSING` status per `docs/features/FEATURES.md`.

Nothing here touches real files. Every "operation" mutates in-memory mock state only.

## Files

| File | Responsibility |
|---|---|
| `index.html` | App shell: titlebar, command box, sidebar nav, content region, dialog/toast roots |
| `styles.css` | Layout + components per the UI/UX foundation pack; extended `--fm-*` vars derived from `branding/colors.json` |
| `app.js` | Icons, theme (System/Light/Dark), hash router, runtime store, mock engine (`FM.engine`), shared UI helpers |
| `mock-data.js` | Mock dataset mirroring `src/FileMind.Domain/Contracts/*` field-for-field |
| `screens.js` | The 7 approved screens: Dashboard, Files, Tools, Search, Devices, Activity, Settings |
| `flows.js` | Organizer operation flow, Console simulator, Architecture screen |

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
