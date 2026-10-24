# Migrations
Every schema change has a numbered migration, forward validation, and backup/recovery strategy. Never edit an already released migration. Tests must open old fixture databases and migrate them to current schema.
