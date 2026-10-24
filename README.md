# FileMind

> Offline-first, privacy-first, reversible smart file organizer for Windows and Android. Backend-first implementation.

## Purpose
FileMind organizes files locally using deterministic rules and classification. Optional Groq AI may suggest, but only the deterministic `Decision -> Safety -> ActionPlan -> Platform Adapter -> Verify -> History` pipeline may mutate files. See `docs/packs/foundation-pack/` and `docs/packs/backend-roadmap/` for the approved product and backend intent.

## Status
M0–M3 implemented and verified (see `docs/plans/PLAN-001-MEGAPLAN.md`): domain contracts/engines, application pipeline, Windows FS adapter, SQLite persistence, console host. Branding palette adopted per `docs/plans/PLAN-002-MEGAPLAN.md` (`branding/` canonical tokens + `FileMind.Branding` constants). 90 tests pass. Check `docs/features/FEATURES.md` for per-feature status labels. Windows automation/watchers, intelligence, Groq, Android, search, and UI are not implemented yet.

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

## Development Principle
**Audit → Plan → Approve → Implement → QA → Repair → Document → Commit.**

Do not describe proposed or planned functionality as implemented. The codebase must be inspected to establish actual behavior.
