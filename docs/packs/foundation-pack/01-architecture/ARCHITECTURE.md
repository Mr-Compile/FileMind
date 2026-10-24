# Architecture

## Style
Clean Architecture + MVVM at clients + Ports/Adapters + job/event-driven processing.

## Layers
1. Presentation: WinUI 3 / Jetpack Compose.
2. Application: use cases, commands, orchestration, jobs.
3. Domain: classification, rules, collections, confidence, decisions, safety policies, action plans.
4. Infrastructure: SQLite, parsers, metadata, search, hashing, Groq provider.
5. Platform adapters: Windows filesystem APIs / Android SAF + MediaStore.

## Core pipeline
FileCandidate -> Normalize -> Local Signals -> Rule Engine -> Context Classifier -> Confidence -> optional AI -> Decision -> Safety -> ActionPlan -> Platform Adapter -> Verify -> History.

## Technology
### Windows first
- C# / .NET 10
- WinUI 3
- MVVM
- SQLite
- FileSystemWatcher
- Windows credential storage

### Android
- Kotlin
- Jetpack Compose
- MVVM
- Room/SQLite
- Storage Access Framework
- MediaStore
- WorkManager
- Android Keystore

### Shared contracts
- JSON schemas for rules/profiles/actions/classifications.
- SQLite-compatible conceptual data model.
- Rust shared core is optional later, only after contracts are stable.

## Key interfaces
IFileSystemAdapter, IClassifier, IRuleEngine, IMetadataExtractor, IContentExtractor, IHashService, ISearchIndex, IAIProvider, IActionPlanner, ISafetyValidator, IHistoryRepository, IJobQueue.
