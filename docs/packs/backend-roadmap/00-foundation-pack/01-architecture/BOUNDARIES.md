# Architectural Boundaries

- Domain cannot import WinUI, Android, SAF, MediaStore, FileSystemWatcher, or OS-specific APIs.
- AI provider cannot execute filesystem actions.
- UI cannot directly move/copy/delete files.
- Application use cases orchestrate; they do not contain platform I/O details.
- Platform adapters validate real permissions immediately before execution.
- All file mutations must originate from an ActionPlan.
- All completed mutations must emit history/audit records.
- Destructive cleanup/delete features are separate from organization and require explicit policy/confirmation.
