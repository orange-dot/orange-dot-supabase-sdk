# Lifecycle State Machine

The orchestration lifecycle is modeled as explicit states rather than constructor side effects and prose ordering requirements.

## States

| State | Meaning |
|-------|---------|
| `Configured` | Options accepted, no session load attempted |
| `Hydrated` | Persisted session load attempted |
| `Initialized` | Child clients constructed and runtime wiring complete |
| `Ready` | Public operations may execute against child clients |
| `Disposed` | Terminal state |

## Refinement Note

The public lifecycle above is intentionally small.

The hosted lifecycle TLA+ model keeps the public shell gate together with the hosted startup coordinator. It makes `LoadingSession`, `Initializing`, `Canceled`, `Faulted`, and the pre-publish overlap explicit so stop-before-publish ordering can be checked without changing the public orchestration shape.

## Transition Graph

```text
Configured
  | LoadPersistedSessionAsync()
  v
Hydrated
  | InitializeAsync(ct)
  v
Initialized
  | bootstrap complete
  v
Ready

Configured  -----------\
Hydrated    ------------> Disposed
Initialized -----------/
Ready ----------------/
```

## Transition Table

| From | Event | To |
|------|-------|----|
| `Configured` | `LoadPersistedSessionAsync()` | `Hydrated` |
| `Hydrated` | `InitializeAsync(ct)` | `Initialized` |
| `Initialized` | bootstrap completion | `Ready` |
| `Configured` | `Dispose()` | `Disposed` |
| `Hydrated` | `Dispose()` | `Disposed` |
| `Initialized` | `Dispose()` | `Disposed` |
| `Ready` | `Dispose()` | `Disposed` |

## Canonical Invariant References

- `INV-LC-001` — no public child call before `Ready`
- `INV-LC-002` — `Ready` never regresses
- `INV-LC-003` — `Disposed` is terminal and idempotent
- `INV-LC-004` — lifecycle ordering is load -> initialize -> ready

## Verification Targets

- Type-level API should make illegal transition sequences unrepresentable where possible.
- Runtime readiness gate should block public operations until `Ready`.
- Replay tests only need to cover the runtime readiness gate that the type system does not prove on its own.
