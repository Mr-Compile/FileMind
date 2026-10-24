# Schemas

Versioned JSON contracts shared across platforms. Every document carries `schemaVersion` (integer, >= 1).

- `rules/rule.schema.json` — deterministic organize rules (conditions, actions, priority, conflict policy).
- `profiles/organizer-profile.schema.json` — portable profile: rules, collections, subjects, projects, aliases, naming templates, exclusions, portable preferences. Platform-only settings (Windows paths, Android URI grants) are excluded by design.

Policy: unknown/newer `schemaVersion` fails safely — no partial import. Changes to a published schema version are forbidden; bump `schemaVersion` instead.
