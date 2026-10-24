# Data Model

Suggested tables/entities:
files, file_metadata, file_content_index, collections, categories, subjects, subject_aliases, projects, project_aliases, rules, rule_conditions, rule_actions, classifications, smart_inbox_items, operations, operation_items, protected_paths, exclusions, watched_folders, duplicate_groups, duplicate_items, profiles, settings, jobs, ai_requests.

## Migration rules
- Every schema has a version.
- Migrations are forward-tested and backup-aware.
- Rule/profile JSON carries schemaVersion.
- Unknown newer profile versions fail safely rather than partially importing.

## Identity
Do not rely on path alone as permanent identity. Track path plus platform-appropriate stable hints where available; revalidate before actions.
