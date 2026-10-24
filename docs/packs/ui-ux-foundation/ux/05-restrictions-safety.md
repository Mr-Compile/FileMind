# Restrictions & Safety

Guard:
- protected/system files
- app-owned/internal data
- permission boundaries
- read-only locations
- destructive bulk actions
- active/in-use files
- unsupported device/file operations
- destination loops
- insufficient space
- disconnected/removable storage
- duplicate overwrites

## Destructive tiers
Low: easily reversible → lightweight confirmation/undo.
Medium: bulk or potentially disruptive → explicit confirmation.
High: permanent or protected → consequence-focused confirmation; require deliberate action.

Never use scary warnings for harmless operations.
