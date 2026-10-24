# Core Contracts

## FileCandidate
id, sourceUri/path, displayName, extension, mime, size, createdAt, modifiedAt, sourceScope, platformHints.

## ClassificationResult
collection, category, subcategory, subject, project, fileKind, confidence, evidence[], source(local/rule/ai), explanation.

## ActionPlan
planId, createdAt, sourceDecision, items[], requiresReview, dryRun, reversibleEstimate.

## ActionItem
operation(move/copy/rename/create-folder/tag/ignore), source, destination, conflictPolicy, preconditions, reason.

## SafetyResult
allowed, blockers[], warnings[], requiredConfirmation.

## OperationRecord
operationId, planId, timestamps, item states, original locations/names, resulting locations/names, verification, undo capability.

## Rule
id, name, enabled, priority, conditions[], actions[], mode, createdAt, updatedAt, schemaVersion.

## AI request contract
Send only fields permitted by privacy policy. Prefer filename/metadata/extracted snippets over raw file bytes. Require structured JSON response and validate against schema before use.
