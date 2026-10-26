# Glossary

Terms as implemented in the codebase (Android Kotlin mirrors C# names).

| Term | Meaning |
|---|---|
| **FileCandidate** | A scanned file: source URI, display name, extension, MIME, size, modified time, metadata. |
| **ClassificationResult** | Per-file verdict: `fileKind`, `subject`, `project`, `confidence`, `evidence`, `warnings`, `needsReview`. |
| **Subject** | A user catalog entry (id, name, code, aliases, keywords) matched against filenames/content — e.g. a course. Managed in Settings → Subjects. |
| **Project** | A work/project catalog entry (id, name, client, aliases, keywords). |
| **Collection** | A named destination group (`school`, `work`) carrying a `destinationTemplate` and member subject/project ids. |
| **Decision** | Per-file outcome: `Organize` / `Review` / `Ignore` / `NoOp` + destination/naming templates + conflict policy + reasons. |
| **ActionPlan / ActionItem** | The executable plan: move/copy/rename items with source→destination, preconditions, conflict policy, undo recipe. |
| **UndoRecipe** | Per-item reverse operation (dest → source + fingerprint) enabling verified undo. |
| **OperationRecord** | A persisted run: items with per-item status (Planned…Committed/Failed/NeedsAttention/RolledBack). |
| **NeedsAttention** | Terminal-but-unresolved status: run ended with items that failed, conflicted, or were cancelled; partially-committed items remain undoable. |
| **Smart Inbox** | Review queue for Review decisions (low confidence or rule-required confirmation). |
| **SAF grant** | Android persisted tree URI permission (`takePersistableUriPermission`) scoping all SAF writes; URIs under a grant are tree-scoped (`tree/<grant>/document/<leaf>`). |
| **Dry run** | A full pipeline pass (plan + safety) that moves nothing — preview only. |
| **Destination template** | Folder pattern with tokens (`{Subject}`, `{FileKind}`, `{Collection}`, `{YYYY}`, `{RelativeDir}`, …) resolved per file. |
| **Naming template** | Filename pattern (`{Name}`, `{Title}`, `{Author}`, `{Ext}`, …) for smart renames. |
| **Mode** | Manual / Smart / Automatic (+AiAssisted/LocalOnly) — how much runs without per-run review. |
| **organized/** | The per-source output root FileMind creates beneath the scanned folder; excluded from subsequent scans. |
| **Preserve tree** | Option recreating the source-relative subdirectory inside the destination template. |
