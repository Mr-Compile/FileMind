# Project Rules

## Source of Truth
The implementation must be audited before making claims about current behavior. Approved product/backend intent lives in `docs/packs/foundation-pack/` and `docs/packs/backend-roadmap/`; repo rules live in `AGENTS.md` and `docs/rules/`.

## Rule Authority
Do not invent rules. A rule must be traceable to explicit user direction or an approved repository rules document. The extracted packs in `docs/packs/` are approved intent sources.

## Approved Backend Rules
From `docs/packs/backend-roadmap/AGENTS.md` and `docs/packs/foundation-pack/09-ai-agent/AI_DEVELOPMENT_RULES.md`:
- Domain/core logic must not call OS filesystem APIs directly.
- Every mutation must originate from an ActionPlan and pass the Safety Engine; verified and recorded in history.
- Classifiers and AI only return structured recommendations; never mutate.
- Never silently overwrite. Protected/system/application paths denied by default.
- Groq is optional; failure/offline/rate limits must never break local organization.
- Android access limited to authorized/OS-supported storage mechanisms.
- Add tests with each feature; do not postpone safety tests.
- Implement only the active phase; preserve module boundaries; small interfaces, explicit result/error types, deterministic local logic, versioned schemas, migrations, idempotent jobs, structured logs.
- No hard-coded API keys.

## Approved Mockup Rule
`mockup/` is the canonical visual reference of the approved UI and must be updated in the same change whenever UI-relevant code, contracts, host verbs, feature statuses, brand tokens, or ui-ux-foundation intent change. See `docs/rules/MOCKUP_RULES.md`.

## Fact Labels
Use: `EXISTING`, `MISSING`, `BROKEN`, `INCOMPLETE`, `INCONSISTENT`, `LEGACY`, `PROPOSED`, `UNVERIFIED`.

## Organization Safety Rules (product-level, verified intent)
- Every filesystem mutation originates from an `ActionPlan`, passes the
  Safety Engine, verifies per-item, and records history with an undo
  recipe. No other code path moves/copies/renames/deletes files.
- Never silently overwrite: conflicts resolve via explicit policy
  (KeepBoth / Skip / Ask / Replace-with-confirmation).
- Never destructively delete inside the core organizer. Cleanup is a
  separate feature requiring explicit user confirmation by design.
- Platform permissions are authoritative: Android writes stay inside
  persisted SAF tree grants (tree-scoped document URIs); protected and
  system paths are denied by default.
- Partially-completed runs are first-class: they end `NeedsAttention`,
  stay recoverable, and their committed items remain undoable. Do not
  equate "incomplete" with "currently running".
- Decisions must be explainable — every organize/skip carries reasons and
  evidence the UI can surface.

## Offline & AI Boundaries
- The core workflow (scan → classify → plan → organize → verify → undo →
  history) must function fully without connectivity.
- AI providers are advisory only: they return structured suggestions and
  never mutate files. Absent/denied/offline AI must degrade to the local
  deterministic result — never to a failure of the workflow.

## Change Discipline
Repair before replacing. Reuse before duplicating. Plan before coding. Approval before implementation. QA before completion. Documentation before final handoff. Logical commits after verified work — auto-commit on verified completion per `AGENTS.md`.
