# Test Vectors

This directory holds canonical JSON scenarios for future replay tests and executable-spec checks.

## Canonical Shape

Each vector uses this top-level shape:

```json
{
  "id": "domain_001_example",
  "title": "Short scenario title",
  "initial_state": {},
  "events": [
    { "type": "some_event" }
  ],
  "expected": {},
  "invariants": ["INV-..."],
  "notes": "Optional implementation note"
}
```

## Domains

- `lifecycle/` holds only the readiness-gate scenario that is not already implied by typed lifecycle states
- `auth/` holds the high-signal behavioral scenarios: late replay, refresh ordering, stale rejection, and sign-out race
- `urls/` models endpoint derivation from normalized base URLs

## Conventions

- `initial_state` and `expected` are domain-specific but stable within a domain.
- `events` are ordered and replay-oriented.
- `invariants` must reference IDs from [`../invariants.md`](../invariants.md).
- Binding names use `Postgrest`, `Realtime`, `Storage`, and `Functions`.
- URL vectors use a single base URL and expect all derived endpoints from that same normalized base.
- Trivial restatements of the typed lifecycle API and read-only policy assertions should stay out of this directory unless they drive a real replay test.
