# Architecture

## Current Architecture
No code exists yet — implementation pending under `docs/plans/PLAN-001-MEGAPLAN.md`. This file will be updated to describe verified code after each milestone.

## Approved Architecture Intent (PROPOSED until implemented)
Source: `docs/packs/foundation-pack/01-architecture/` and `docs/packs/backend-roadmap/`.

- Style: Clean Architecture + MVVM at clients + Ports/Adapters + job/event-driven processing.
- Layers: Presentation (WinUI 3 / Compose later) → Application (use cases, orchestration, jobs) → Domain (classification, rules, collections, confidence, decisions, safety, action plans) → Infrastructure (SQLite, parsers, hashing, search, Groq) → Platform adapters (Windows FS APIs / Android SAF+MediaStore).
- Core pipeline: `FileCandidate -> Normalize -> Local Signals -> Rule Engine -> Context Classifier -> Confidence -> optional AI -> Decision -> Safety -> ActionPlan -> Platform Adapter -> Verify -> History`.
- Solution layout: `src/FileMind.{Domain,Application,Infrastructure.Sqlite,Intelligence.Local,Intelligence.AI,Platform.Windows,Windows.Host}` + `tests/` per `docs/packs/backend-roadmap/11-implementation/REPOSITORY_TREE.md`.

## Boundaries (approved intent)
- Domain cannot import WinUI, Android, SAF, MediaStore, FileSystemWatcher, or OS-specific APIs.
- AI provider cannot execute filesystem actions. UI cannot move/copy/delete files.
- All mutations originate from an ActionPlan and pass the Safety Engine; all completed mutations emit history records.
