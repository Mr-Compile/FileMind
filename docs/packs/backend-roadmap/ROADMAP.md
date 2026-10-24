# Backend Roadmap

## Phase 1 — Core Engine
Create domain models, classifier pipeline, rule engine, confidence engine, ActionPlan, safety policies, transaction contracts, repositories, and platform interfaces. No real filesystem mutation from domain code.

## Phase 2 — Windows Backend
Implement Windows filesystem adapter, scanner, metadata provider, operation executor, local persistence, and manual end-to-end organization.

## Phase 3 — Windows Automation
Add watch folders, event queue, debounce/stability checks, download completion handling, background jobs, notifications contracts, startup/tray service contracts, and removable-drive events.

## Phase 4 — Backend QA
Stress permissions, locked files, name conflicts, disappearing files, full disks, crashes, retries, huge scans, removable-drive loss, rule conflicts, and undo/rollback.

## Phase 5 — File Intelligence
Add plugin-style extractors for PDF/DOCX/PPTX/text, image EXIF, audio/video metadata, richer local classification, and smart rename.

## Phase 6 — Groq AI
Add AIProvider + GroqProvider, privacy gate, prompt/request shaping, response schema validation, confidence/review policy, quotas/rate limits, cache, audit trail, and offline fallback.

## Phase 7 — Android Backend
Implement SAF/MediaStore adapters, persisted folder grants, WorkManager jobs, Room/SQLite persistence, Android share input, and platform-specific safety checks while reusing shared specifications.

## Phase 8 — Search
Add local indexing and SQLite FTS5. Natural-language/semantic features remain optional AI enhancements.

## Phase 9 — Cross-Platform
Versioned profile export/import for rules, collections, subjects, projects, aliases, naming templates, exclusions, and preferences. Do not sync user files in the initial design.

## Phase 10 — Advanced Intelligence
Correction learning, suggestions, relationships/version intelligence, optional local models, semantic index, and multi-provider AI.
