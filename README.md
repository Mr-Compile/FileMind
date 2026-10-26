# FileMind

> Smart, offline-first, reversible file organization for Windows and Android — scan, classify, build folder structures, move safely, verify, undo.

## Purpose
FileMind is a smart automatic file **organization** system — not a file manager. It analyzes your files (type, name, metadata, content, subject codes), builds a meaningful structure under `organized/` (e.g. `School/IT-412/Document/…` or `Files/Image/…`), moves files through a safety-checked, transactional pipeline, and keeps every run undoable. Optional Groq AI may suggest, but only the deterministic `Decision -> Safety -> ActionPlan -> Platform Adapter -> Verify -> History` pipeline may mutate files. Approved intent: `docs/packs/foundation-pack/` + `docs/packs/backend-roadmap/`; product bible: `docs/product/PRODUCT.md`.

## Status
Both native clients are implemented and verified:

- **Windows** (`src/FileMind.WinUI`, WinUI 3 + `src/FileMind.Windows.Host` console): full pipeline, watch automation, FTS5 search, Groq advisory layer, duplicates/storage, profile import/export, correction learning — verified on dev-signed builds (`docs/qa/QA_EVIDENCE_PLAN-014.md`). 314 tests pass.
- **Android** (`android/`, Jetpack Compose + Room + WorkManager, `com.filemind.app`): verified **on-device** (Infinix X6725B, Android 15) — SAF grant flow, recursive scans, subject-based routing (`School/{Subject}/{FileKind}`), Moves under tree-scoped permissions, undo of partial runs, Smart Inbox, rename, Activity needs-attention separation, organizer state preservation + run-in-progress nav guard. Subjects are managed in Settings → Subjects.
- **Mockup** (`mockup/`): dependency-free interactive HTML mirror of both UIs; kept in sync per `docs/rules/MOCKUP_RULES.md`.

Check `docs/features/FEATURES.md` for per-feature status labels (`EXISTING`/`INCOMPLETE`/`PROPOSED`/…).

**UI preview:** `mockup/` is a dependency-free interactive HTML/CSS/JS mockup of the approved UI — open `mockup/index.html` in a browser (works over `file://`). It drives the real pipeline semantics (scan → analyze → plan → safety → execute → verify → undo) on mock data and must stay in sync with code per `docs/rules/MOCKUP_RULES.md`.

## Build & Test (verified on Windows 11 + .NET SDK 10.0.402)

```bash
dotnet build FileMind.slnx --artifacts-path <outdir>   # 0 warnings, 0 errors
dotnet test  FileMind.slnx --artifacts-path <outdir>   # 314 tests pass
# If Smart App Control blocks fresh test binaries: use the byte-loading
# runner — dotnet fsi tools/sac-test-runner.fsx -- <outdir>/bin/<Tests>/debug …
```

Android:

```bash
cd android && ./gradlew.bat installDebug   # needs JAVA_HOME (Android Studio JBR)
# unit tests: ./gradlew.bat :core:test :platform:testDebugUnitTest
```

**Note:** `--artifacts-path` is required on machines where Smart App Control / WDAC blocks unsigned binaries under `C:\wamp64\www` or flags new test DLLs (e.g. `--artifacts-path "$env:USERPROFILE\filemind-artifacts"`). `tests/FileMind.Windows.IntegrationTests` is excluded from the solution for this reason — see its README.

Run the console host:

```bash
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll seed-demo
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll analyze <dir>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll execute <dir> --dest <outroot> --dry-run
```

Run the Windows app (Smart App Control note):

```bash
dotnet build src/FileMind.WinUI -c Debug   # produces a self-contained win-x64 apphost
```

On machines with Smart App Control enforcement, unsigned `FileMind.*.dll`
loads are blocked — sign the output with the dev cert helper
`powershell tools/fm-sign.ps1 <output-dir>` (cert generated once per dev
box, CurrentUser store) before launching `FileMind.WinUI.exe`. MSIX
packaging (`Package.appxmanifest` + `makeappx`) works but installation
needs the cert trusted at machine level (`Cert:\LocalMachine\Root`, admin).

## Documentation
- `AGENTS.md` — mandatory AI-agent workflow and repository truth rules.
- `docs/packs/foundation-pack/` — approved product/architecture/safety intent (source pack).
- `docs/packs/backend-roadmap/` — approved backend implementation intent (contracts, engines, phases, test matrices).
- `docs/packs/branding-palette/` + `docs/packs/ui-ux-foundation/` — branding + UI/UX intent packs.
- `branding/` — canonical brand color tokens (`colors.json`, `colors.css`) and brand rules.
- `docs/product/` — purpose, users, scope, terminology.
- `docs/rules/` — non-negotiable project rules.
- `docs/architecture/` — verified system architecture (updated as code lands).
- `docs/features/` — canonical feature specifications and statuses.
- `docs/plans/` — plan-before-code documents.
- `docs/qa/` — QA and acceptance validation.
- `docs/development/` — build, contribution, release, troubleshooting.
- `mockup/` — interactive static preview of the approved UI (open `index.html`, no build needed).

## Development Principle
**Audit → Plan → Approve → Implement → QA → Repair → Document → Commit.**

Do not describe proposed or planned functionality as implemented. The codebase must be inspected to establish actual behavior.
