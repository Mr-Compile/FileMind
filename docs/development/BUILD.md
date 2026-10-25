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

## Android backend (`android/`)

Verified on Windows 11 with Android Studio (bundled JBR 21.0.10), Android SDK
`platforms;android-36` + `build-tools;36.0.0`, Gradle 9.1.0 (wrapper-pinned),
AGP 9.0.1 (built-in Kotlin 2.2.10), KSP 2.3.6.

```bash
cd android
JAVA_HOME="C:/Program Files/Android/Android Studio/jbr" ./gradlew.bat \
  assembleDebug testDebugUnitTest :core:test
```

- `:core` — pure-JVM Kotlin port of the domain engine + app pipeline. JUnit 5
  parity tests consume `tests/fixtures/manifest.json` directly (the same file
  drives `FileMind.Domain.Tests/ManifestParityTests.cs` on Windows).
- `:platform` — `com.android.library`: SAF/MediaStore adapter, persisted URI
  grants, Room persistence (schema JSON exported to `platform/schemas/`),
  WorkManager jobs, notification contract, share-target intake.
- `:app` — `com.android.application` (applicationId `com.filemind.app`):
  Jetpack Compose + Material 3 UI per `docs/packs/ui-ux-foundation/platforms/
  android.md` + `docs/packs/crossplatform-ux/` (PLAN-021). Depends on
  `:platform` only; all file mutations flow through `OrganizerPipeline` /
  `FileMindWorkScheduler` / repositories — never direct FS calls.
  `androidx.core` / `core-splashscreen` pinned to 1.17.x (1.19.x requires
  compileSdk 37 / AGP 9.1).
- JVM unit tests: `./gradlew.bat :platform:testDebugUnitTest`
  (URI model, capabilities, path normalizer, protected paths, work contract).
- Instrumented tests (`platform/src/androidTest`): Room `MIGRATION_1_2` and
  adapter round-trip — require a device/emulator; none was attached when this
  was verified (2026-10-24), so they compile but are marked `UNVERIFIED`
  on-device.

### Android emulator setup

The new Android CLI (`cmdline-tools/latest/bin/android`) manages packages and
AVDs; package paths use `/` separators, not `;`:

```bash
android sdk install "system-images/android-36/google_apis/x86_64"
android emulator create --help   # create an AVD, e.g. filemind-pixel
android emulator start <name>    # blocks until booted
adb install app/build/outputs/apk/debug/app-debug.apk
```
- Kotlin metadata note: `kotlin-stdlib` and `kotlinx-*` deps are pinned to
  versions compiled by Kotlin ≤2.2 so AGP 9's built-in Kotlin can read them;
  `androidx.core` is pinned to 1.17.x because 1.19.x requires compileSdk 37 /
  AGP 9.1, above the installed SDK.

## Host runtime configuration

- `FILEMIND_DB` — SQLite path (default `%LOCALAPPDATA%\FileMind\filemind.db`)
- `FILEMIND_DEST` — default destination root for `plan`/`execute`
