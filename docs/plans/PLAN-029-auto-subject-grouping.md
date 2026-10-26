# PLAN-029 — Auto subject-group inference (F-P12)

Status: APPROVED (user instruction "update the codebase to follow this" after the
documentation task documented F-P12 as PROPOSED with this exact intent).

## Intent (from user + bible)

When a scanned folder contains many files sharing a course-code-like filename
pattern (`IT-412`, `IT421`, `CS 301`, …), the organizer should group them into a
per-subject folder **without** requiring a manually configured subject.

## Approach

- New `SubjectClusterer` (pure, deterministic, offline): scans the batch's
  display names for bounded course-code patterns `[A-Za-z]{2,5}[-_ ]?[0-9]{3,4}[A-Za-z]?`
  with word boundaries; canonical key `"LETTERS-DIGITS".uppercase()` (`IT-412`).
- Groups with `>= 2` files become inferred subjects. Camera/download stems
  (`img`, `pxl`, `dsc`, `screenshot`, `wa`, `received`, …) are denied.
- In `OrganizerPipeline.analyze`, when classification produced no subject and
  the filename contains a detected code, set `subject = <code>` with an
  `inferred-subject` evidence row passed through `extraEvidence` (score 60,
  near catalog code-match 65) so clustered files reach the High band and
  auto-organize; `context-over-type` (+20) and `NeedsReview` are rebaked after
  the subject is applied. Existing `School/{Subject}/{FileKind}` routing then
  applies.
- Inferred subjects are ephemeral per-run — nothing is written to the subjects
  catalog; users can still pin real subjects via Settings → Subjects.
- Mirrored implementation on both platforms (parity rule) + unit tests.

## Verification

- `SubjectClustererTests` (C#, 7) + `SubjectClustererTest` (Kotlin, 6): green.
- `WindowsEndToEndTests.InferredSubject_AutoGroupsWithoutCatalog`: MATH-210
  cluster (incl. nested subfolder) → `organized/School/MATH-210/{Kind}/`,
  ungrouped file untouched: green.
- Mockup mirrors the same regex/deny-list/score in `FM.engine.detectClusters`.
- Android device run: pending.

## Non-goals

No persistent catalog writes, no fuzzy/ML clustering, no rename changes.
