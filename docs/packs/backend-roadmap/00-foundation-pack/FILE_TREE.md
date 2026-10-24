# Suggested Repository Shape

filemind/
  apps/
    windows/
      FileMind.App/
      FileMind.Application/
      FileMind.Windows/
      FileMind.Tests/
    android/
      app/
      domain/
      data/
      platform/
  core/
    contracts/
    schemas/
    fixtures/
  docs/

Windows-first note: initially domain/application may live as clean C# projects. Do not add Rust FFI until cross-platform contracts are proven and the maintenance benefit clearly exceeds complexity.
