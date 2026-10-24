# Product Bible

## Product Summary
FileMind is an offline-first smart file organizer with two native clients: Windows (C#/.NET 10, WinUI 3 later) and Android (Kotlin/Compose later). Local rules and classification are primary; Groq AI is an optional intelligence fallback. Approved intent: `docs/packs/foundation-pack/00-foundation/FOUNDATION.md`.

## Problem
Files accumulate unmanaged across Downloads, Documents, and media folders. Manual organization is tedious; cloud organizers sacrifice privacy and offline use. FileMind organizes locally, reversibly, and explainably.

## Target Users
See `TARGET_USERS.md`. Initial focus: Windows users with structured collections (School subjects, Work projects, Development, Media) who need deterministic, undoable organization.

## Product Principles
From `docs/packs/foundation-pack/` (approved intent):
- Offline-first; internet is optional.
- Local rules/classification primary; Groq optional fallback.
- Safe-by-default file operations; preview/dry-run before risky or bulk changes.
- Every mutation logged and designed for undo where technically possible.
- Never silently overwrite.
- Low-confidence classifications go to review (Smart Inbox).
- Platform permissions are authoritative.
- Shared behavior via contracts/schemas, not duplicated assumptions.
- No platform filesystem code inside classification/domain logic.

## Core Capabilities
Planned per `docs/packs/foundation-pack/02-features/FEATURES_BY_PHASE.md` — statuses tracked in `docs/features/FEATURES.md`. All are `MISSING`/`PROPOSED` until implemented.

## Non-Goals
- Cloud file sync (profile data only; user files never synced).
- Destructive cleanup/delete inside the core organizer (separate feature, explicit policy).
- AI-driven filesystem mutation (AI advises; never executes).
- UI design in the backend phases (separate UI/UX pack).
