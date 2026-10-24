# Phase 6 — Groq AI

Groq is an optional online intelligence provider, not the organizer authority.

Components: AIProvider interface, GroqProvider, privacy/consent gate, request builder, response schema validator, confidence mapper, retry/rate-limit policy, request budget, cache, audit metadata, secure credential abstraction, and offline fallback.

Allowed purposes: ambiguous classification, context/subject/project suggestions, destination/category/collection suggestions, rename suggestions, rule generation from natural language, query interpretation, explanations, and Smart Inbox assistance.

Flow:
`Local Result -> low/medium confidence + AI enabled -> privacy gate -> minimize payload -> Groq -> validate structured response -> merge as evidence -> confidence/review policy -> ActionPlan -> Safety`.

Never let model output directly invoke filesystem methods. Never hard-code a shared production API key in distributed binaries. File contents require explicit privacy settings; prefer extracted/minimized text over uploading whole files.
