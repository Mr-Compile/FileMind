# FileMind — Product Bible

This is the canonical product bible. Approved implementation intent lives in
`docs/packs/foundation-pack/` and `docs/packs/backend-roadmap/`; this document
defines *why* FileMind exists and *what it is intended to become*.

> PRODUCT INTENT ≠ IMPLEMENTED FEATURE. For the verified current state, see
> `docs/features/FEATURES.md` and `docs/architecture/`.

## Identity

FileMind is a smart, offline-first, cross-platform **automatic file
organization system** for Android and Windows. It is not a file manager:
it does not primarily browse or edit files — it analyzes them, classifies
them, creates meaningful folder structures, moves them safely, and verifies
the result.

## Problem

Files accumulate unmanaged across Downloads, Documents, media, and shared
folders. Manual organization is tedious and inconsistent; cloud organizers
sacrifice privacy and require connectivity. Users end up with thousands of
files they cannot find, sorted into structures that made sense only on the
day they were built.

## Purpose

FileMind reduces the manual work of organization to a review-and-approve
step: scan → understand → classify → create structure → organize → verify.
It produces an understandable, explainable, reversible folder structure —
locally, without uploading user files.

## Target Users

See `TARGET_USERS.md` for the full list. In short: students (course/subject
codes are a first-class signal), developers, office workers, content
creators, and anyone with large or poorly organized storage. FileMind is
**not** exclusively a student app — its architecture is general-purpose.

## Platforms

- **Windows**: NTFS paths, `FileSystemWatcher`, full-volume access, desktop
  app (WinUI 3) + console host.
- **Android**: scoped storage — SAF persisted tree grants, MediaStore,
  app-private storage; Jetpack Compose app + WorkManager background runs.

Shared behavior is expressed through contracts and shared fixtures
(`tests/fixtures/manifest.json`), never assumed identical: platform
permissions are authoritative on each side. Android cannot do everything
Windows can (and vice versa) — see `docs/architecture/PLATFORM.md`.

## Core Workflow

`Scan → Understand → Classify → Create Structure → Organize → Verify`

Concretely: scan files (recursively, including subfolders) → analyze
(type, name, metadata, content where available) → classify (kind, subject,
project, confidence) → decide per rules/collections → plan (destination
templates, rename templates, conflict policy, undo recipe) → safety check
→ execute transactionally (verify each item) → record in history.

## Organization Philosophy

- **Explainable**: every decision carries evidence and reasons; users can
  ask "why?" and get an answer, not a mystery.
- **Deterministic where practical**: same inputs → same plan, every time.
- **Predictable**: destination templates (`School/{Subject}/{SubKind}`,
  `{KindFolder}`, user-defined) are visible before execution.
- **Preview-first**: plans are shown (with a dry-run option) before files
  move; bulk operations confirm.
- **Reversible**: every item's move records an undo recipe; committed
  items — including partial runs — are reversible.
- **Safe-by-default**: protected paths denied, never silently overwrite,
  no destructive deletion inside the core organizer.

## Folder Generation

FileMind creates the folders its decisions require **inside the selected
folder** (in-place organize — no `organized/` wrapper), including nested
structure (subject folders with school-activity buckets, kind folders,
preserved source trees when enabled). Re-running on the same folder is
idempotent: files already at their resolved destination produce no item,
and the managed roots (`School/`, `Documents/`, `Images/`, `Videos/`,
`Music/`, `Archives/`, `Work/`, `Other/`) are excluded from rescans.
Conceptual example (not a mandated layout):

```
<selected folder>/
  School/IT-421/Assignments/IT421_hw1.pdf
  School/IT-421/Notes/IT-421 reviewer.docx
  School/CS-301/Labs/CS301_lab3.docx
  Documents/report.pdf
  Music/track.mp3
  Archives/pack.zip
```

Structure follows the user's classification signals, collections, and
rules — not a fixed universal shape.

## Smart Classification

FileMind goes beyond extension sorting, using layered signals:

- **Kind**: extension + MIME → Document, Image, Video, Audio, Archive,
  Code, Spreadsheet, Presentation, …
- **Context**: subject codes/names/aliases/keywords (e.g. `IT-412`,
  `it412`, `IT 412` all match) and project names beat generic type
  routing ("context-over-type").
- **Content** (where supported): local extraction (OOXML/PDF text, ID3,
  EXIF, MP4, ZIP manifest, code signals) feeds extra evidence at reduced
  weight.
- **Confidence**: evidence scores map to High/Medium/Low bands; weak
  classifications go to Smart Inbox review rather than being moved blind.

Auto-deriving subject groupings from raw filenames without a configured
subject catalog (e.g. "many files share `IT-` prefixes → propose a
subject folder automatically") is `PROPOSED` — see FEATURES.md F-P12.

## Offline-First Core

Scanning, classification, rule evaluation, planning, folder creation,
organization, verification, undo, search index, and history run fully
locally. No network is required for the core workflow.

## Optional AI

Groq (or future providers) is an **optional online enhancement layer**
only: ambiguous-classification advice, suggestion drafts, natural-language
rules/search interpretation. AI advises — the deterministic
`Decision → Safety → ActionPlan → Adapter → Verify → History` pipeline is
the only code path allowed to mutate files. When AI is absent, offline,
rate-limited, or denied consent, FileMind falls back to the local result.
AI status: `EXISTING` on Windows (advisory only, gated, quota'd);
`PROPOSED` on Android.

## Safety

- Validation before every mutation: source exists, fingerprint unchanged,
  destination writable, sufficient space, conflicts resolved by policy
  (KeepBoth / Skip / Ask / Replace-with-confirmation — never silent).
- Platform permissions are authoritative: SAF tree grants scope Android
  writes; protected/system paths are denied by default.
- Transactions persist per-item state; crashes reconcile to
  `NeedsAttention`, never to lost half-state.
- No automatic destructive deletion — ever. Cleanup is a separate feature
  requiring explicit user design and confirmation.
- Every mutation is logged to operation history with an undo recipe.

## User Control

Manual / Smart / Automatic modes let the user choose how much automation
runs without review. Users can preview plans, dry-run, confirm bulk ops,
exclude paths, write rules, manage subjects/projects/collections, resolve
Smart Inbox items, and undo committed work — including partially-completed
runs.

## Scope

In scope: scanning, understanding, classifying, structuring, organizing,
verifying, undoing files on local storage; user-defined rules; subjects/
projects/collections; Smart Inbox review; duplicates *detection* (advisory);
storage analysis; profile import/export; optional AI advice.

Out of scope (non-goals): cloud file sync of user files; destructive
cleanup/delete in the core organizer; AI-driven filesystem mutation;
general file management (edit/view/share); social features; system
cleaners; mandatory connectivity.

## Current State

`EXISTING` (verified): the full pipeline on Windows (console + WinUI app)
and Android (Compose app, on-device verified incl. SAF moves, undo of
partial runs, per-subject routing via a Subjects manager); local
intelligence; watch automation (Windows); FTS5 search (Windows); Groq
advisory layer (Windows); profile import/export; correction learning;
duplicates + storage reports; HTML mockup parity shell.

`PROPOSED`: automatic subject-group inference from filename clusters;
subjects/projects manager in the mockup; Explorer shell integration;
NL search interpretation; live-Groq verification; Android watch mode.

See `FEATURES.md` for the per-feature matrix — statuses are evidence-based
and only updated after verification.

## Future Direction

Richer context understanding (auto-grouping, semantic relations, OCR),
broader platform integration (Explorer, Android widgets/share), provider
registry for multiple AI backends, and correction-learning that turns
undos into better rules — each under its own approved plan.
