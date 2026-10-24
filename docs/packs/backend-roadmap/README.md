# FileMind — Complete Backend Roadmap Pack

Implementation-ready planning pack for FileMind. This pack starts from the existing Foundation Pack and carries the backend through the Core Engine, Windows integration, automation, QA, local file intelligence, Groq AI, Android, search, cross-platform profiles, and advanced intelligence.

## Build order
1. Foundation
2. Core Engine
3. Windows Backend
4. Windows Automation
5. Backend QA / Hardening
6. Local File Intelligence
7. Groq AI
8. Android Backend
9. Search
10. Cross-Platform Profiles
11. Advanced Intelligence

## Non-negotiable architecture
`Input -> Analysis -> Decision -> Safety -> ActionPlan -> Platform Adapter -> Verify -> History`

AI, UI, classifiers, and rules never directly mutate the filesystem.

## Initial technology direction
- Windows: C# / .NET 10, WinUI 3 later, SQLite
- Android: Kotlin, Jetpack Compose later, Room/SQLite, SAF, MediaStore, WorkManager
- Rules/profiles: versioned JSON schemas
- Search: SQLite FTS5 first
- AI: Groq behind an AIProvider abstraction; optional and online-only
- Shared native core: do not introduce Rust during the first Windows implementation. Re-evaluate after contracts are proven and Android sharing requirements are known.
