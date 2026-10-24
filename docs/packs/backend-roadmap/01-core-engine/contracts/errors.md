# Error Model
Use stable error codes plus human-safe messages.

Families: validation, permission, protected-path, conflict, not-found, locked/in-use, insufficient-space, unsupported, transient-IO, persistence, verification, rollback, AI/offline/rate-limit, cancellation.

Errors must state whether retry is safe and whether manual review is required. Never swallow filesystem errors.
