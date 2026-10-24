# Phase 8 — Search

Start local: file index + metadata + extracted text + SQLite FTS5. Search services support filename, collection, subject, project, category, type, date, metadata, and full-text filters.

Indexing is asynchronous, cancellable, incremental, and resilient to moved/deleted files. Natural-language query interpretation may use Groq, but the underlying retrieval remains local. Semantic/vector indexing is a later opt-in capability.
