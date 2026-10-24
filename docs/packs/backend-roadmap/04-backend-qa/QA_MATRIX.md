# Phase 4 — Backend QA / Hardening

Required suites:
- Unit: normalizer, matchers, confidence, rules, destination templates, conflict policy.
- Contract: all FileSystemAdapter implementations obey the same semantics.
- Integration: SQLite, migrations, Windows adapter, transaction recovery.
- Fixture E2E: School, Work, Development, Downloads, Media, duplicates, messy names.
- Stress: 10k/50k+ indexed files, burst watcher events, cancellation, restart.
- Failure injection: permission denied, locked file, source disappears, disk full, DB failure, crash boundaries, USB removal.
- Recovery: resume/reconcile persisted operations and jobs.

Release gate: no known silent data-loss path; unsafe ambiguity becomes review; undo/rollback is tested for every mutating operation supported in the phase.
