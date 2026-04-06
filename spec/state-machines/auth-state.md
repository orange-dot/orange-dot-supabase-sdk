# Auth State Machine

The orchestration layer treats auth as canonical state owned in one place and projected outward to child clients.

## States

| State | Meaning |
|-------|---------|
| `Anonymous` | No authenticated session |
| `Authenticated` | Current access token and refresh token are valid |
| `Refreshing` | Refresh in progress for the current canonical session version |
| `SignedOut` | Session explicitly cleared |
| `Faulted` | Auth transition failed and requires caller-visible handling |

## Transition Graph

```text
Anonymous ---- sign-in success -----------------> Authenticated
Authenticated ---- refresh start --------------> Refreshing
Refreshing ---- refresh success ---------------> Authenticated
Refreshing ---- refresh failure ---------------> Faulted
Authenticated ---- sign-out -------------------> SignedOut
Faulted ---- recover / re-auth ----------------> Authenticated
SignedOut ---- sign-in success ----------------> Authenticated
```

## Rules

- Canonical auth state carries a monotonically increasing session version.
- A newer session version always wins over an older refresh result.
- Sign-out clears all child-client auth projections.
- New subscribers must immediately observe the current canonical auth state.
- Refresh does not grant a second source of truth; it proposes a new version to the same authority owner.

## Refinement Note

The TLA+ model keeps `pending_refresh_version` explicit and treats binding projection as asynchronous steps. That refinement is used to model refresh-vs-sign-out races and eventual convergence of live bindings.

## Canonical Invariant References

- `INV-AM-001` — canonical auth state has one authority owner
- `INV-AU-001` — late subscribers immediately observe the latest canonical auth state
- `INV-AU-002` — `SignedOut` clears all live projections
- `INV-AU-003` — stale refresh results cannot overwrite a newer canonical state
- `INV-BD-001` — projected versions never exceed canonical version

## Verification Targets

- Property tests for replay, sign-out clearing, and newest-version-wins behavior.
- TLA+ model for ordering and eventual propagation across live bindings.
