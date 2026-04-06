# Invariants

This document is the canonical invariant list for the orchestration-layer prototype.

Other spec artifacts should reference invariant IDs from this file rather than restating the same rule with different wording.

## Authority Ownership

| ID | Statement |
|----|-----------|
| `INV-AM-001` | Canonical auth/session state has exactly one authority owner inside the orchestration layer. |
| `INV-AM-002` | Child clients consume projected session state but do not define canonical session truth. |

## Lifecycle

| ID | Statement |
|----|-----------|
| `INV-LC-001` | No public operation may execute a child-client call before `Ready`. |
| `INV-LC-002` | `Ready` never regresses to a pre-ready lifecycle state. |
| `INV-LC-003` | `Disposed` is terminal and idempotent. |
| `INV-LC-004` | Lifecycle ordering is `LoadPersistedSessionAsync()` before `InitializeAsync(ct)` before `Ready`. |

## Auth State

| ID | Statement |
|----|-----------|
| `INV-AU-001` | A new subscriber immediately observes the latest canonical auth state. |
| `INV-AU-002` | `SignedOut` clears all live child-client auth projections. |
| `INV-AU-003` | A stale refresh result cannot overwrite a newer canonical session version or a later sign-out. |

## Binding Projection

| ID | Statement |
|----|-----------|
| `INV-BD-001` | A binding's projected version can never exceed the canonical session version. |
| `INV-BD-002` | Bindings project canonical auth state but do not mutate canonical auth state. |

## Observability

| ID | Statement |
|----|-----------|
| `INV-OB-001` | Observability has no command path into lifecycle or auth-state mutation. |

## URL Derivation

| ID | Statement |
|----|-----------|
| `INV-URL-001` | Endpoint derivation is deterministic for a given base URL. |
| `INV-URL-002` | Auth, REST, Realtime, Storage, and Functions endpoints derive from the same normalized base URL. |
