# Backend Routing

## Manual scan
`ScanFolder -> Scanner -> FileCandidate -> Enrichment -> Classifier -> Rules -> ActionPlanner -> Safety -> Preview -> ExecutePlan -> Adapter -> Verify -> Persist`.

## Automatic watch
`Platform Event -> Queue -> Debounce/Stability -> Scan Candidate -> Classify -> Rules -> Plan -> Safety -> Mode Policy -> Execute OR Smart Inbox`.

## AI fallback
`Classification -> Confidence Gate -> AI Privacy Gate -> AIProvider -> Structured Result Validation -> Evidence Merge -> Decision -> Plan -> Safety`.

## Undo
`UndoOperation -> Load committed operation -> Reconcile current filesystem -> Build reverse plan -> Safety -> Execute -> Verify -> Record undo relationship`.

## Startup recovery
`Start -> DB migrations -> load incomplete operations/jobs -> reconcile filesystem -> resume/rollback/NeedsAttention -> start watchers/jobs`.
