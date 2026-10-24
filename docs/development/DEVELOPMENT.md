# Development

Verified prerequisites, commands, and developer workflows. Do not record unverified steps.

## Prerequisites
- Windows 11, .NET SDK 10.0.402 (target framework `net10.0`; solution file `FileMind.slnx`).
- Any modern browser for the UI mockup — no runtime dependencies.

## Build & Test

```bash
dotnet build FileMind.slnx --artifacts-path <outdir>   # 0 warnings, 0 errors
dotnet test  FileMind.slnx --artifacts-path <outdir>   # builds the suite
```

`--artifacts-path` is required on machines where Smart App Control / WDAC blocks unsigned binaries under `C:\wamp64\www` — see root README note and `docs/development/BUILD.md`.

When SAC reputation-blocks the freshly built `FileMind.*.dll`s themselves (event 3077, observed on this machine), run the suite through the byte-loading runner — identical IL, verified 108/108:

```bash
dotnet fsi tools/sac-test-runner.fsx -- <outdir>/bin/FileMind.Domain.Tests/debug \
  <outdir>/bin/FileMind.Branding.Tests/debug <outdir>/bin/FileMind.Application.Tests/debug
```

## Console host

```bash
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll seed-demo
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll scan <dir>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll analyze <dir>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll plan <dir> --dest <root>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll execute <dir> --dest <root> [--dry-run] [--apply]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll history [count]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll undo <operationId>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll smart-inbox [resolve <id>]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll mode [manual|smart|automatic|ai-assisted|local-only]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll rules [list|show <id>|test <id|file> <dir>|import <file>|export <id> --out <file>]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll watch add <dir> [--mode <mode>]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll watch remove <dir>
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll watch list
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll watch [run] [--seconds N] [--apply]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll watch-status
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll enrich <dir> [--shallow]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll dupes [<prefix>] [--plan] [--apply] [--dest <root>]
dotnet <outdir>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll storage [<prefix>]
```

`enrich` runs the local-intelligence analyzers (OOXML/PDF/EXIF/ID3/video/archive/code) and prints extracted metadata + evidence per file; extraction is bounded (`ExtractionBudget`) and fail-safe.

`watch run` consumes `FileSystemWatcher` events through a bounded queue + `IClock` debounce, re-stats each path (`FileStabilityChecker` — `.crdownload/.part/.partial/.download/.tmp` never move), then routes through `ModePolicy` per watched folder (`watch add --mode` persists `watch.mode.<path>` in settings). Removable-drive attach/detach pauses/resumes watchers on affected roots.

`dupes` reads the file index and groups exact duplicates via size → partial-hash → full-hash (over-cap files stay partial-only, flagged unconfirmed) plus similar/version candidates with evidence; advisory only. `dupes --plan` builds a review-first quarantine plan (`duplicates-review`, Move + KeepBoth, SafetyEngine-validated, preview-only); `--apply` executes through `TransactionEngine` and is undoable. `storage` prints kind/age-band breakdown, largest/oldest, and the duplicate-reclaimable estimate.

If SAC blocks the host dll too, launch it via `dotnet fsi tools/sac-run.fsx -- <path-to-host.dll> <verb> ...`.

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
