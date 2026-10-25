# FileMind

> Offline-first, privacy-first, reversible smart file organizer for Windows and Android. Backend-first implementation.

## Purpose
FileMind organizes files locally using deterministic rules and classification. Optional Groq AI may suggest, but only the deterministic `Decision -> Safety -> ActionPlan -> Platform Adapter -> Verify -> History` pipeline may mutate files. See `docs/packs/foundation-pack/` and `docs/packs/backend-roadmap/` for the approved product and backend intent.

## Status
M0–M3 implemented and verified (see `docs/plans/PLAN-001-MEGAPLAN.md`): domain contracts/engines, application pipeline, Windows FS adapter, SQLite persistence, console host. Branding palette adopted per `docs/plans/PLAN-002-MEGAPLAN.md` (`branding/` canonical tokens + `FileMind.Branding` constants). The native Windows app (`src/FileMind.WinUI`, WinUI 3) is implemented and verified on a dev-signed unpackaged build — see `docs/plans/PLAN-014-MEGAPLAN.md` + `docs/qa/QA_EVIDENCE_PLAN-014.md`. The native Android app (`android/app`, Jetpack Compose + Material 3) builds and packages a debug APK; emulator runtime verification is tracked in `docs/plans/PLAN-021-MEGAPLAN.md`. Check `docs/features/FEATURES.md` for per-feature status labels.

**UI preview:** `mockup/` is a dependency-free interactive HTML/CSS/JS mockup of the approved UI — open `mockup/index.html` in a browser (works over `file://`). It drives the real pipeline semantics (scan → analyze → plan → safety → execute → verify → undo) on mock data and must stay in sync with code per `docs/rules/MOCKUP_RULES.md`.

## Build & Test (verified on Windows 11 + .NET SDK 10.0.402)

```bash
dotnet build FileMind.slnx --artifacts-path <outdir>   # 0 warnings, 0 errors
dotnet test  FileMind.slnx --artifacts-path <outdir>   # 90 tests pass
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
