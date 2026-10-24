# Phase 1 — Core Engine

Goal: make FileMind's backend deterministic, testable, safe, and platform-independent before touching real files.

Deliverables: domain contracts, normalization/classification pipeline, collections/subjects/projects, rule engine, confidence engine, safety policy engine, ActionPlan, transaction/undo contracts, repositories, migrations plan, platform ports, fixtures, and automated tests.

Exit gate: given synthetic FileCandidate inputs, the core can classify, evaluate rules, produce explainable ActionPlans, reject unsafe plans, and describe undo information without invoking Windows/Android APIs.
