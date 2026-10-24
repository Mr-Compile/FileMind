# AGENTS.md — FileMind Backend Rules

## Mission
Build a privacy-first, offline-first, reversible smart file organizer for Windows and Android. The product must remain useful without AI.

## Mandatory boundaries
- Domain/core logic must not call OS filesystem APIs directly.
- Classifiers and AI only return structured recommendations/results.
- Every mutation must originate from an ActionPlan and pass the Safety Engine.
- Every successful mutation must be verified and recorded in history.
- Destructive or ambiguous behavior defaults to review, not guessing.
- Never silently overwrite an existing file.
- Protected/system/application paths are denied by default.
- Groq is optional; failure/offline/rate limits must never break local organization.
- Android access is limited to authorized/OS-supported storage mechanisms.
- Add tests with each feature; do not postpone safety tests.

## Development style
Implement vertically but preserve module boundaries. Prefer small interfaces, explicit result/error types, deterministic local logic, versioned schemas, migrations, idempotent jobs, and structured logs. No UI work is required during the foundation/backend phases.

## Definition of Done
A backend feature is done only when contracts, validation, failure paths, tests, logging, persistence implications, undo/rollback implications, and documentation are addressed.
