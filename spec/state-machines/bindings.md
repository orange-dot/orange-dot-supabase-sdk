# Binding Projection Rules

Bindings translate canonical auth state into child-client-specific runtime configuration.

## Binding Types

| Binding | Projection |
|---------|------------|
| PostgREST | request headers |
| Realtime | access token |
| Storage | request headers |
| Functions | request headers |

## Rules

- Bindings are consumers of canonical auth state, not owners of it.
- A binding subscribes to the auth-state observer and immediately receives the latest state.
- `Authenticated(version, token)` updates all live bindings to that version.
- `SignedOut` clears all live bindings.
- Starting a binding late must replay the latest canonical state without requiring a new auth transition.

## Canonical Invariant References

- `INV-AM-002` — child clients consume projected state but do not define canonical truth
- `INV-AU-001` — late subscribers replay the latest auth state
- `INV-AU-002` — `SignedOut` clears live projections
- `INV-BD-001` — projected version never exceeds canonical version
- `INV-BD-002` — bindings do not mutate canonical auth state
- `INV-OB-001` — observability attached to a binding has no command path
