# Auth State Machine

The orchestration layer treats auth as canonical state owned in one place and projected outward to child clients.

## States

| State | Meaning |
|-------|---------|
| `Anonymous(version=0)` | No authenticated session |
| `Authenticated(version, token)` | Current access token and refresh token are valid for the canonical session version |
| `Refreshing(version, pending_refresh_version)` | Refresh is being projected for the current canonical session version |
| `SignedOut(version)` | Session explicitly cleared while preserving the last canonical version |
| `Faulted(version, pending_refresh_version)` | Auth transition failed and requires caller-visible handling |

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

The TLA+ model keeps `pending_refresh_version` explicit and treats binding projection as asynchronous steps. The current implementation also keeps version and pending-refresh metadata in the bridge layer. Because upstream Gotrue emits refresh completion but not a dedicated refresh-start event, the orchestration layer synthesizes `Refreshing` immediately before publishing the refreshed `Authenticated` state so the public model still exposes the full transition vocabulary.

## Canonical Invariant References

- `INV-AM-001` — canonical auth state has one authority owner
- `INV-AU-001` — late subscribers immediately observe the latest canonical auth state
- `INV-AU-002` — `SignedOut` clears all live projections
- `INV-AU-003` — stale refresh results cannot overwrite a newer canonical state
- `INV-BD-001` — projected versions never exceed canonical version

## Verification Targets

- Property tests for replay, sign-out clearing, and newest-version-wins behavior.
- TLA+ model for ordering and eventual propagation across live bindings.
