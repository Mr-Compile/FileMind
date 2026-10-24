# FileMind.Windows.IntegrationTests

**Status: temporarily excluded from `FileMind.slnx`.**

This project hosts the Windows end-to-end tests (real `WindowsFileSystemAdapter`
+ real SQLite on disposable directories). The E2E tests currently live in
`tests/FileMind.Application.Tests/WindowsEndToEndTests.cs` so they run on
machines where Smart App Control (WDAC policy `0283AC0F-...`) blocks unsigned
test binaries by reputation.

Why: on the current dev machine, SAC is in enforcement mode and consistently
denies this assembly on load (Code Integrity event 3077), while the other test
assemblies pass. Once binaries are Authenticode-signed, or SAC is turned off,
move `WindowsEndToEndTests.cs` back here and re-add the project to `FileMind.slnx`.
