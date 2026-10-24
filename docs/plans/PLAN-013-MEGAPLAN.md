# PLAN-013 — FileMind Advanced Intelligence Megaplan (Backend Roadmap Phase 10 / PLAN-001 M10b)

Status: APPROVED
Related Feature: F-P11 (`PROPOSED` — see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Cover `docs/packs/backend-roadmap/10-advanced-intelligence/PLAN.md` (zip `02_FileMind_Complete_Backend_Roadmap.zip`, outline in PLAN-001 M10): candidate post-stable-product capabilities — correction-derived suggestions, adaptive confidence, related-file detection, version intelligence, semantic index, predictive organization, optional local models, multiple AI providers, cross-device profile sync.

## Repository Audit

### Verified Existing State

- F-P11 is `PROPOSED` in FEATURES.md — the pack itself frames these as candidates "after the stable product", not commitments.
- Prerequisites that must land first: PLAN-005 (automation events feed learning signals), PLAN-007 (extraction evidence), PLAN-008 (similarity plumbing), PLAN-009 (provider infrastructure), PLAN-011 (local index; semantic index builds on it), PLAN-012 (profile sync base).
- `EXISTING` foundations: rule provenance field (`Rule.Provenance`), confidence banding, audit table shell, Smart Inbox review flow (partial).
- `MISSING`: learning store, correction capture, adaptive confidence, related/version detection, semantic/vector index, local model hosting, provider registry.

### Evidence / Relevant Files

- Spec: `10-advanced-intelligence/PLAN.md`; `src/FileMind.Domain/Contracts/Rule.cs` (provenance), `ConfidenceEngine.cs`.

### Documentation vs Code Differences

None — candidate-phase intent.

## Findings

- Pack intent: `EXISTING` (candidate list, guardrail).
- All capabilities: `MISSING` / `PROPOSED` — each milestone below is independently gated; nothing here activates without its own scope approval.

## Global Constraints (verbatim from pack)

Guardrail: learned behavior creates suggestions or versioned rules — it must never silently weaken safety or protected-path policies. All learned artifacts carry provenance (`RuleProvenance.Learned`-style marking) and are review-gated before activation. Local models optional + offline-capable; no telemetry.

## Proposed Changes / Implementation Phases

Each milestone is independently approvable; this plan may be split per capability when activated.

### Milestone 1 — Correction Learning (ACTIVATED 2026-10-24 — IMPLEMENTED)

Exit gate: user corrections recorded → suggestion candidates generated as versioned, provenance-marked draft rules; nothing auto-activates.
- 1.1 DONE — `corrections` table (schema v5) + `ICorrectionRepository`; `CorrectionService.ObserveUndoAsync` (executed undo = correction, wired into `OrganizerPipeline.UndoAsync`) + `learn record` (manual overrides); `MineSuggestionsAsync` groups by (ext, chosen dir) ≥2 → `Rule` with `Provenance=Learned`, `Enabled=false`, surfaced via `learn suggest`. Activation path = existing rule verbs — review-gated. Verified: `LearningTests` ×6 + e2e.

### Milestone 2 — Adaptive Confidence (ACTIVATED 2026-10-24 — IMPLEMENTED)

- 2.1 DONE — `confidence_calibration` table + `IConfidenceCalibrationRepository`; `ConfidenceCalibrator` (±5/signal, ±20 clamp, score stays [0,100]) applied in `AnalyzeAsync` keyed `ext:<ext>`; `Rejected` recorded on executed undo; `learn calibration` verb. Caps/bands/safety untouched — verified by `LearningTests.Calibrator_*` + `LearnedRule_Enabled_StillBlockedBySafety`.

### Milestone 3 — Related/Version Intelligence (ACTIVATED 2026-10-24 — IMPLEMENTED)

- 3.1 DONE — `RelatedFileService` over the file index: `v(N)`, ` (N)` copy, and year-version chains; unversioned same-base sibling = order 0; `related [path]` verb prints chains suggestion-only. Verified: `LearningTests.Related_*` + e2e.

### Milestones 4–6 — still PROPOSED, not activated

Semantic index (M4), provider registry + local models (M5), cross-device profile sync (M6) remain candidate scope — not implemented.

### Milestone 4 — Semantic Index (candidate, opt-in)

- 4.1 Embedding-based index on extracted text; local models first; builds on PLAN-011 FTS5; explicit opt-in + storage cost disclosure.

### Milestone 5 — Provider Registry + Local Models (candidate)

- 5.1 Multi-provider `IAIProvider` registry (Groq + local model hosts); same privacy gate + budgets for all.

### Milestone 6 — Cross-Device Profile Sync (candidate)

- 6.1 Conflict-explicit sync of `organizer-profile.json` only (never user files); preserves local safety settings; depends on PLAN-012.

## Non-Goals

Anything that bypasses SafetyEngine; silent auto-learning into live rules; cloud file sync; shipping unvetted model binaries.

## Files Expected to Change

TBD per activated milestone — Domain/Application/Intelligence.* surfaces plus docs. Each activated milestone gets its own expanded plan per AGENTS.md.

## Security / Privacy Impact

Learning data is local; semantic index opt-in with disclosure; all providers under one privacy gate; learned rules review-gated.

## Error Handling / Edge Cases

Poisoned corrections (adversarial patterns) → suggestion-only output + provenance + caps; model absence → graceful absence; index drift → rebuild.

## QA Plan

Per-capability: suggestion correctness tests, provenance verification, safety-invariant regression (protected paths stay denied regardless of learned state), determinism where applicable.

## Acceptance Criteria

- [ ] Each activated capability ships suggestions/versioned rules only — no direct mutation
- [ ] Learned artifacts carry provenance + review gate
- [ ] Safety/protected-path invariants hold under learned state (regression tests)
- [ ] F-P11 remains `PROPOSED` until a capability is verified; then granular rows added

## Risks / Regression Areas

Scope creep (candidate phase) → per-milestone activation gate; learned-safety weakening → invariant tests; model supply-chain → vetted sources only.

## Documentation Updates

`FEATURES.md` F-P11 (stays PROPOSED until verified), `CHANGELOG.md` per activated milestone.

## Commit Plan

Per activated milestone: `feat(intelligence-*):` + `test(*):` + `docs(*):`.

## Approval

User approval required before implementation; each milestone requires re-confirmation of scope when activated.
