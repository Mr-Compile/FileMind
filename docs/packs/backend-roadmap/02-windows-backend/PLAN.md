# Phase 2 — Windows Backend

Implement in C#/.NET 10 behind the core ports.

Deliverables:
- WindowsFileSystemAdapter: enumerate/stat/move/copy/rename/create-directory/capability checks.
- Windows scanner with cancellation, progress, recursion policy, exclusions, and symlink/reparse-point policy.
- SQLite repositories + migrations.
- Metadata provider and stable file identity/fingerprint strategy.
- Manual command/application services: ScanFolder, AnalyzeFile, PlanOrganization, ExecutePlan, UndoOperation.
- Structured logs and operation verification.

End-to-end route:
`Command -> Application Use Case -> Core -> ActionPlan -> Safety -> Windows Adapter -> Verify -> History`.

Exit gate: organize a disposable fixture directory safely, preview the exact plan, execute it, restart the app/backend, inspect history, and undo it.
