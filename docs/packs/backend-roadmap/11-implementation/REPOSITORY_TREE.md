# Suggested Repository Tree

```text
filemind/
├── AGENTS.md
├── README.md
├── docs/
├── src/
│   ├── FileMind.Domain/
│   ├── FileMind.Application/
│   ├── FileMind.Infrastructure.Sqlite/
│   ├── FileMind.Intelligence.Local/
│   ├── FileMind.Intelligence.AI/
│   ├── FileMind.Platform.Windows/
│   └── FileMind.Windows.Host/
├── android/                  # introduced at Android phase
│   ├── app/
│   ├── domain-contracts/
│   ├── data/
│   └── platform/
├── schemas/
│   ├── rules/
│   └── profiles/
└── tests/
    ├── FileMind.Domain.Tests/
    ├── FileMind.Application.Tests/
    ├── FileMind.Windows.IntegrationTests/
    └── fixtures/
```

Do not create Rust crates until a measured cross-platform need justifies FFI/JNI complexity.
