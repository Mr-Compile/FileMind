# Offline Rules

Verified: the entire core workflow runs locally. The only network-facing
component is the optional Groq advisory layer (`FileMind.Intelligence.AI`),
which degrades to the local result when unavailable.

## Mandatory

- Scanning, classification, rules, decisions, planning, folder creation,
  move/copy/rename, verification, undo, indexing, search, and history
  **must not require connectivity**.
- No feature may block on a network call in the core pipeline. AI calls
  run off the mutation path (advice merges evidence; deterministic code
  still decides and executes).
- AI unavailability (no key, offline, rate-limited, consent denied) must
  produce the same deterministic local outcome — never an error state.
- Secrets/keys never ship in source; provider keys live in platform
  secret stores (`EnvSecretStore`, `WindowsSecretStore`/DPAPI).
- New dependencies must be justified against the offline-first rule;
  network-requiring features are opt-in and marked `PROPOSED` until
  approved.
