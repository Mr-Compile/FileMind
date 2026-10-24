# Feature Catalog

Use stable feature IDs where practical. Each feature must clearly distinguish `EXISTING`, `INCOMPLETE`, `BROKEN`, `MISSING`, `PROPOSED`, and `UNVERIFIED` states. A feature document is not proof that implementation exists.

Source of intent: `docs/packs/foundation-pack/02-features/FEATURES_BY_PHASE.md`. All features below are approved intent with no implementation yet.

| ID | Feature | Status | Spec |
|---|---|---|---|
| F-P1 | Core Foundation: scan, type/MIME detect, filename analysis, basic classification, destinations, move/copy/rename, SQLite index, history, undo, conflicts, protected paths, exclusions, dry run, preview | INCOMPLETE — scan/classify/move/copy/rename/SQLite ops history/undo/conflicts/protected paths/exclusions/dry-run+plan preview EXISTING and E2E-verified; file-index repo exists but hashing/indexing not wired into pipeline; crash-recovery reconciler MISSING | `docs/packs/foundation-pack/02-features/` + `docs/packs/backend-roadmap/01-core-engine/` + `02-windows-backend/` |
| F-P2 | Smart Organization: collections, categories, subjects/aliases/keywords, project aliases, context routing, confidence, Smart Inbox, Manual/Smart/Automatic modes | INCOMPLETE — subjects/aliases/code matching, project matching, confidence bands, context-over-type routing, Review decisions EXISTING; Smart Inbox repo exists but Review decisions not yet persisted into it; organization modes MISSING | same + `docs/packs/backend-roadmap/01-core-engine/classifier/` |
| F-P3 | Rule Engine: JSON rules, conditions/actions, priorities, validation, conflicts, test/preview, import/export, templates | INCOMPLETE — engine (priority/specificity/stop/conflict detection), condition eval, validator, JSON schema, SQLite persistence EXISTING; no rule authoring UI, no rule-level preview command, profile import/export covers rules but no standalone rule-file import | `docs/packs/backend-roadmap/01-core-engine/rules/` |
| F-P4 | Windows Automation: watch folders, stable-file/download checks, event queue/debounce, background operation, notifications, external drives, Explorer contracts | MISSING | `docs/packs/backend-roadmap/03-windows-automation/PLAN.md` |
| F-P5 | File Intelligence: PDF/DOCX/PPTX/TXT extraction, metadata/EXIF/audio/video, content classification, smart rename | MISSING | `docs/packs/backend-roadmap/05-file-intelligence/PLAN.md` |
| F-P6 | Duplicates & Storage: size prefilter, hashing, duplicate groups, large/old analysis, storage breakdown | MISSING | `docs/packs/foundation-pack/02-features/` P6 |
| F-P7 | Groq AI: optional provider, ambiguous classification, suggestions, NL rules/search, privacy controls, limits, fallback | MISSING | `docs/packs/backend-roadmap/06-groq-ai/PLAN.md` |
| F-P8 | Android: SAF/MediaStore, WorkManager, notifications, share target, history/undo, shared formats | MISSING | `docs/packs/backend-roadmap/07-android-backend/PLAN.md` |
| F-P9 | Search: SQLite FTS5, filters, NL query interpretation | MISSING | `docs/packs/backend-roadmap/08-search/PLAN.md` |
| F-P10 | Cross-Platform: profile import/export/backup/restore, shared rules/collections/templates | MISSING | `docs/packs/backend-roadmap/09-cross-platform/PLAN.md` |
| F-P11 | Advanced: local AI, semantic index, correction learning, adaptive confidence | PROPOSED | `docs/packs/backend-roadmap/10-advanced-intelligence/PLAN.md` |
| F-B1 | Branding: canonical palette tokens (`branding/colors.json`, `colors.css`, `BRAND.md`), `FileMind.Branding` C# mirror + parity tests | EXISTING — verified 2026-10-24 (`branding/` + `src/FileMind.Branding` + `tests/FileMind.Branding.Tests`) | `docs/packs/branding-palette/` + `docs/plans/PLAN-002-MEGAPLAN.md` |
| F-B2 | Logo/icon production assets + platform handoffs | PROPOSED — blocked on user candidate selection (`branding/brand-boards/candidate-board-1.png`) | `docs/packs/branding-palette/{windows,android}/HANDOFF.md` |
| F-UI1 | Interactive HTML mockup of approved UI (`mockup/`): spec screens, pipeline demo, console simulator, architecture view; canonical visual reference per `docs/rules/MOCKUP_RULES.md` | INCOMPLETE — PLAN-003 approved, in progress | `docs/packs/ui-ux-foundation/` + `docs/plans/PLAN-003-MEGAPLAN.md` |
| F-B1 | Branding palette tokens: canonical `branding/colors.json` + `colors.css`, C# `FileMind.Branding` constants, parity tests | EXISTING | `branding/` + `docs/packs/branding-palette/` + `docs/plans/PLAN-002-MEGAPLAN.md` |
| F-B2 | Logo + icon assets: master SVG, PNG/ICO, Android adaptive icon, splash assets | PROPOSED (blocked on candidate selection) | `docs/packs/branding-palette/{windows,android}/HANDOFF.md`, `branding/brand-boards/` |
| F-UI1 | UI/UX foundation: design language, tokens, themes, components, screens, UX flows, platform specs | MISSING (intent extracted, no implementation) | `docs/packs/ui-ux-foundation/` |
