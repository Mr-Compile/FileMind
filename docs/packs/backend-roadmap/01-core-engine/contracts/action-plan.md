# ActionPlan
Immutable proposal for a filesystem change.

Fields: plan ID; operation (move/copy/rename/create-folder/tag/ignore/notify/ask); source; destination; reason; confidence; originating rule/classification; preconditions; conflict policy; required capabilities; undo recipe; risk flags.

Only a platform executor may execute an approved plan after Safety Engine validation.
