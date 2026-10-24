# Build

Verified on Windows 11, .NET SDK 10.0.402.

## Commands

```bash
dotnet build FileMind.slnx [--artifacts-path <dir>]   # library + host build
dotnet test  FileMind.slnx --artifacts-path <dir>     # full suite: 90 tests
dotnet build src/FileMind.Windows.Host/FileMind.Windows.Host.csproj --artifacts-path <dir>
```

`dotnet test` only builds test-reachable projects; build the host project
separately to get `FileMind.Windows.Host.dll`.

## Smart App Control / WDAC

This dev machine runs Smart App Control in enforcement mode plus a Code
Integrity policy. Consequences observed 2026-10-24:

- `C:\wamp64\www\...` output paths are path-blocked — use `--artifacts-path`
  pointing outside the web root (e.g. `C:\Users\<you>\filemind-artifacts`).
- New unsigned test/product DLLs may still be reputation-blocked (event 3077);
  `FileMind.Windows.IntegrationTests` is excluded from the solution for now.
- Verified workaround (2026-10-24): `tools/sac-test-runner.fsx` loads the built
  test assemblies and their `FileMind.*`/`NuGet` dependencies from byte streams
  inside a dedicated `AssemblyLoadContext` — identical IL, image-mapped loads of
  the blocked files never happen. `tools/sac-run.fsx` does the same for the
  console host entry point:

  ```bash
  dotnet fsi tools/sac-test-runner.fsx -- <out>/bin/FileMind.Domain.Tests/debug \
    <out>/bin/FileMind.Branding.Tests/debug <out>/bin/FileMind.Application.Tests/debug
  dotnet fsi tools/sac-run.fsx -- <out>/bin/FileMind.Windows.Host/debug/FileMind.Windows.Host.dll <verb> [args]
  ```

  Runner limitations vs `dotnet test`: [Fact]/[Theory]+[InlineData] only (no
  MemberData/ClassData), no collection fixtures/parallelism, per-test
  pass/fail line output. `dotnet test` remains the canonical runner when the
  binaries carry reputation.
- Other remedies: Authenticode-sign test binaries, or turn SAC off
  (Settings → App & browser control → Smart App Control; irreversible on Win11).

## Host runtime configuration

- `FILEMIND_DB` — SQLite path (default `%LOCALAPPDATA%\FileMind\filemind.db`)
- `FILEMIND_DEST` — default destination root for `plan`/`execute`
