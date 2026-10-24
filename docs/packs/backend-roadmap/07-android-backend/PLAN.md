# Phase 7 — Android Backend

Tech: Kotlin, Android SDK, Room/SQLite, Storage Access Framework, MediaStore, WorkManager, Android Keystore. UI is out of scope for this backend phase.

Implement AndroidFileSystemAdapter around authorized URI/document/media capabilities. Persist valid folder grants. Model capability differences explicitly; do not pretend Android has unrestricted path access.

Features: authorized folder scan, Downloads/Documents/Media organization where permitted, scheduled jobs, notifications contracts, Share input routing, history/undo where the underlying provider supports safe reversal, exclusions/protected scopes, and Groq via the same AIProvider behavior.

Exit gate: same core rule/profile fixtures produce equivalent classifications/plans on Windows and Android, while execution respects each platform's permissions.
