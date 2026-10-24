# Validation

Validate as early as useful, not aggressively.

Examples:
- Empty file/folder name
- Invalid/reserved name
- Duplicate/conflicting destination
- Unsupported file
- Missing permission
- Insufficient storage
- Source equals destination
- Missing/disconnected device
- Protected/system location
- Unsafe recursive operation

Messages must say what is wrong and what the user can do next.

Avoid `Invalid input`. Prefer `That name contains a character Windows can't use. Choose a different name.`
