# Build

Verified on Windows 11, .NET SDK 10.0.402.

## Commands

```bash
dotnet build FileMind.slnx [--artifacts-path <dir>]   # library + host build
dotnet test  FileMind.slnx --artifacts-path <dir>     # full suite: 82 tests
dotnet build src/FileMind.Windows.Host/FileMind.Windows.Host.csproj --artifacts-path <dir>
```

`dotnet test` only builds test-reachable projects; build the host project
separately to get `FileMind.Windows.Host.dll`.

## Smart App Control / WDAC

This dev machine runs Smart App Control in enforcement mode plus a Code
Integrity policy. Consequences observed 2026-10-24:

- `C:\wamp64\www\...` output paths are path-blocked — use `--artifacts-path`
  pointing outside the web root (e.g. `C:\Users\<you>\filemind-artifacts`).
- New unsigned test DLLs may still be reputation-blocked (event 3077);
  `FileMind.Windows.IntegrationTests` is excluded from the solution for now.
- Remedies: Authenticode-sign test binaries, or turn SAC off
  (Settings → App & browser control → Smart App Control; irreversible on Win11).

## Host runtime configuration

- `FILEMIND_DB` — SQLite path (default `%LOCALAPPDATA%\FileMind\filemind.db`)
- `FILEMIND_DEST` — default destination root for `plan`/`execute`
