# Test Vectors

This directory holds canonical JSON scenarios used by the replay-model tests under `tests/OrangeDot.Supabase.Tests/Spec/`.

These vectors are stable scenario inputs. They drive the shared auth/lifecycle state-machine layer and URL derivation logic used by the tests, not the production runtime directly.

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

## Schema Validation

Each domain has a JSON Schema under `schemas/`. To validate locally:

```
pip install check-jsonschema
bash scripts/validate-test-vectors.sh
```

The validator checks: schema conformance, ID uniqueness, and invariant reference validity.
