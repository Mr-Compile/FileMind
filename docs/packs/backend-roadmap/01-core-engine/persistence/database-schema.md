# Database Schema
Initial entities: files, file_metadata, collections, categories, subjects, subject_aliases, projects, project_aliases, rules, rule_conditions, rule_actions, operations, operation_items, exclusions, protected_paths, classification_history, watched_folders, profiles, settings, jobs, AI request audit/cache metadata.

Use stable IDs, timestamps, soft/explicit lifecycle states where useful, indexes for common lookups, and schema migrations from day one.
