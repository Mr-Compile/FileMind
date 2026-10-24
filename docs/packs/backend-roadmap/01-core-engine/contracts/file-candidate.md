# FileCandidate
Canonical input presented to the core.

Fields: stable scan ID; source URI/path abstraction; display name; extension; MIME; size; created/modified timestamps; source scope; metadata bag; extracted text reference/snippet; hash state; permission capabilities; platform hints.

Rules: paths/URIs are data, not authority. A FileCandidate cannot mutate itself. Expensive fields may be absent and enriched progressively.
