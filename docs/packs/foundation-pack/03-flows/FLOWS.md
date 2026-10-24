# Backend Flows

## Scan
Request -> permission/path validation -> enumerate -> normalize FileCandidate -> index -> enqueue analysis -> progress/result.

## Classify
FileCandidate -> filename/type/MIME -> metadata -> rules/patterns -> optional content -> context classifier -> confidence -> decision.

## AI fallback
Local result below configured threshold -> privacy policy -> prepare minimal allowed context -> Groq -> parse structured suggestion -> validate -> merge as advisory signal -> decision. If offline/error/rate-limited, continue locally and route uncertain items to Smart Inbox.

## Organize
Decision -> ActionPlan -> dry-run/preview when required -> safety validation -> conflict policy -> execute via platform adapter -> verify -> transaction/history -> index update -> notification.

## Undo
Select operation -> load inverse-capable operation items -> validate current state -> construct reverse ActionPlan -> safety check -> execute -> verify -> append undo transaction. Never pretend an irreversible action is undoable.

## Watch folder
OS event -> debounce -> wait until stable/not partial -> deduplicate event -> scan candidate -> classify -> decision -> Smart/Auto policy -> action plan.

## Duplicate
Size grouping -> optional partial hash -> full hash confirmation -> duplicate group -> review. No automatic deletion by default.

## Search
Query -> parse filters -> local FTS/metadata index -> rank -> return. Optional Groq may interpret natural language into structured search; local index performs retrieval.
