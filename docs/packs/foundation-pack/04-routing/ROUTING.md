# Backend Routing

Routing here means command/use-case routing, not UI navigation.

## Command routes
ScanFolder -> ScanFolderUseCase
AnalyzeFile -> AnalyzeFileUseCase
OrganizeSelection -> OrganizeUseCase
PreviewOrganization -> PreviewUseCase
UndoOperation -> UndoUseCase
CreateRule / UpdateRule / TestRule -> RuleUseCases
SearchFiles -> SearchUseCase
FindDuplicates -> DuplicateUseCase
AnalyzeStorage -> StorageUseCase
ClassifyWithAI -> AIClassificationUseCase
ImportProfile / ExportProfile -> ProfileUseCases

## Event routes
FileDiscovered -> AnalysisQueue
FileChanged -> StabilizationQueue
ClassificationCompleted -> DecisionEngine
NeedsReview -> SmartInboxRepository
ActionPlanApproved -> ExecutionQueue
ActionCompleted -> Verification -> History + IndexUpdate
ActionFailed -> Recovery/Retry policy + History
DriveConnected -> ExternalDrivePolicy
ConnectivityChanged -> AI availability state only; local organizer remains operational.

## Routing rule
Controllers/ViewModels call application use cases only. Use cases depend on interfaces. Infrastructure/platform implementations are injected at composition root.
