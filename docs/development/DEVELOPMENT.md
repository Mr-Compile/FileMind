# Development

Verified prerequisites, commands, and developer workflows. Do not record unverified steps.

## Prerequisites
- Windows 11, .NET SDK 10.0.402 (target framework `net10.0`; solution file `FileMind.slnx`).
- Any modern browser for the UI mockup — no runtime dependencies.

## Build & Test

```bash
dotnet build FileMind.slnx --artifacts-path <outdir>   # 0 warnings, 0 errors
dotnet test  FileMind.slnx --artifacts-path <outdir>   # 90 tests pass
```

`--artifacts-path` is required on machines where Smart App Control / WDAC blocks unsigned binaries under `C:\wamp64\www` — see root README note.

## Console host

```bash
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll seed-demo
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll scan <dir>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll analyze <dir>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll plan <dir> --dest <root>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll execute <dir> --dest <root> --dry-run
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll history [count]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll undo <operationId>
```

Environment variables: `FILEMIND_DB` (SQLite path), `FILEMIND_DEST` (default destination root).

## UI mockup (`mockup/`)

The interactive static mockup needs no build:

- Open `mockup/index.html` directly in a browser (`file://` works), or serve the repo root with any static server and open `/mockup/index.html`.
- Deep links: `#/dashboard`, `#/files`, `#/organizer`, `#/tools`, `#/search`, `#/devices`, `#/activity`, `#/settings`, `#/console`, `#/architecture`. Query params: `?theme=system|light|dark`, `?density=compact`.
- Organizer wizard: pick a source folder → Scan → Analyze → Build action plan → Safety → Execute (dry-run by default) → Result → Undo. The "System folder" source demonstrates a protected-path safety blocker.
- Console screen simulates `filemind` verbs against the same mock dataset.

**Sync rule** (`docs/rules/MOCKUP_RULES.md`): any change to UI/UX behavior, routes, domain/application contracts, pipeline behavior, host commands/output, feature statuses, branding tokens, or the UI-UX foundation pack MUST update the mockup in the same change. Mock data stays contract-shaped (`mock-data.js` cites the C# contract each entity mirrors).

Static checks: `node --check mockup/*.js`. Visual check: screenshot each route, verify status chips carry icon+label (never color alone) in both themes.

## Branding tokens

`branding/colors.json` is canonical; `colors.css` is the runtime CSS linked by the mockup; `src/FileMind.Branding/BrandTokens.cs` is the C# mirror. Parity enforced by `tests/FileMind.Branding.Tests`. Regenerate logo assets via `branding/logo/build.mjs` (Edge headless, no npm deps).
