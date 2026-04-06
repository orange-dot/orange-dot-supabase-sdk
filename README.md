# orange-dot Supabase C# SDK

Date started: 2026-04-05
Status: Planning

## Scope

This repository reimplements the **top-level orchestration layer** of the Supabase C# SDK and keeps the service-specific child modules fixed.

- Child modules are consumed from `supabase-community/*-csharp` as git submodules and pinned so child-module behavior remains fixed during orchestration-layer work.
- Implementation work is limited to the top-level `Supabase/` package surface:
  - `Supabase/Client.cs`
  - `Supabase/StatelessClient.cs`
  - `Supabase/SupabaseTable.cs`
  - URL derivation
  - auth header propagation
  - auth-state observation
  - session lifecycle orchestration
- Work is concentrated at the composition boundary where DI, readiness, observability, endpoint derivation, auth propagation, and client lifecycle behavior are defined.
- Current investigation targets URL derivation, session-load ordering, and auth header propagation fan-out.

## Scope boundaries

### In scope

- Orchestrator client (equivalent to `Supabase.Client`)
- Stateless convenience path (equivalent to `Supabase.StatelessClient`)
- Realtime-aware table wrapper (equivalent to `Supabase.SupabaseTable<T>`)
- URL derivation for Functions / REST / Realtime / Storage endpoints
- Auth header and access-token propagation to child clients
- Auth-state observation and session lifecycle orchestration
- DI integration (`IServiceCollection` extensions)
- Observability integration (`ILogger<T>`, `ActivitySource`, `IMeterFactory`)
- Typed error taxonomy at the orchestration layer
- Unit + integration tests against a local Supabase stack

### Out of scope

- Any change to Gotrue/PostgREST/Realtime/Storage/Functions internals
- Any work on response shapes or query-builder ergonomics inside child modules
- Any NuGet publishing workflow
- Any target outside `net8.0` and `net10.0`
- Any interop layer for F# or VB.NET

### Explicitly deferred

- `supabase-js` feature-parity audit
- Benchmarks against the community implementation

## Child module policy

Submodules under `modules/` are pinned so child-module behavior remains fixed while the orchestration layer changes. Bugs found below the orchestration boundary are handled as separate upstream issues or PRs in the relevant community module and are not patched locally in this repository.

## Design constraints

- `SupabaseOptions` is a standard configure-time options class. It stays mutable during `AddSupabase(...)` / configuration binding, then is treated as immutable after startup.
- Console/manual construction keeps the lifecycle explicit: `SupabaseClient.Configure(options) -> LoadPersistedSessionAsync() -> InitializeAsync(ct)`.
- DI construction uses `services.AddSupabase(...)` plus a hosted startup driver. The resolved `ISupabaseClient` exposes `Task Ready { get; }`, and public operations wait for readiness before first use.
- Auth propagation uses an `IAuthStateObserver` with replay-on-subscribe semantics so late-starting bindings receive the current auth state.
- Observability uses `ILogger<T>`, a static `ActivitySource`, and `IMeterFactory` for counters and histograms.

## Planning notes

- Target implementation window: 5-7 days.
- Prototype scope notes are recorded in [`docs/decision-log.md`](docs/decision-log.md).
- Verification notes currently center on one proved artifact: [`spec/tla/AuthPropagation.tla`](spec/tla/AuthPropagation.tla), with the recorded TLC run in [`spec/tla/README.md`](spec/tla/README.md).

## Documentation

- [`docs/architecture-contrasts.md`](docs/architecture-contrasts.md) — Architectural contrasts against the current orchestration layer, with selected options and code sketches.
- [`docs/implementation-plan.md`](docs/implementation-plan.md) — Build order, cut list, and success criteria.
- [`docs/decision-log.md`](docs/decision-log.md) — Prototype scope and working split.
- [`docs/project-positioning.md`](docs/project-positioning.md) — Repository scope summary.
- [`spec/README.md`](spec/README.md) — Verification-oriented artifacts: invariants, authority model, state machines, verified auth propagation TLA+, and replay vectors.

## Status checklist

- [x] Scope defined
- [x] Architectural contrasts identified
- [x] Repo initialized at `orange-dot/orange-dot-supabase-sdk`
- [x] Verification spec scaffolded
- [x] Auth propagation TLA+ model checked with TLC
- [ ] Submodules wired
- [ ] Solution skeleton + csproj
- [ ] Orchestrator client: construction path + DI
- [ ] Orchestrator client: auth state observable
- [ ] Orchestrator client: typed lifecycle states
- [ ] Observability wiring
- [ ] URL derivation with table-driven tests
- [ ] Stateless client
- [ ] Supabase table wrapper
- [ ] Typed error taxonomy
- [ ] README with public overview
- [ ] Integration tests against local Supabase
