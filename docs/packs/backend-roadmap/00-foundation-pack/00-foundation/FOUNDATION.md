# FileMind Foundation

## Product
One product with two native clients: Windows and Android. Windows ships as a Windows app/installer; Android ships as APK/AAB.

## Principles
- Offline-first; internet is optional.
- Local rules/classification are primary; Groq is an optional intelligence fallback.
- Safe-by-default file operations.
- Preview/dry-run before risky or bulk changes.
- Every mutation is logged and designed for undo where technically possible.
- Never silently overwrite.
- Low-confidence classifications go to review.
- Platform permissions are authoritative.
- Shared behavior is expressed through contracts/schemas, not duplicated assumptions.
- No platform filesystem code inside classification/domain logic.

## Initial modes
Manual, Smart, Automatic, AI-Assisted, Local-Only.

## Suggested confidence policy
- High: auto-organize when mode permits.
- Medium: Smart Inbox review.
- Low: leave untouched.
Thresholds are configurable; initial reference values: >=95 high, 70-94 medium, <70 low.

## V1 boundary
Windows first. Prove scan -> classify -> rules -> action plan -> safety -> execute -> history -> undo before Android or cross-platform core extraction.
