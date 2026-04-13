# SDK Implementation Notes

Date: 2026-04-05
Purpose: record the current repository focus and the implementation boundaries it assumes.

## Repository statement

This repository focuses on the top-level C# client layer for Supabase.

- The target surface is the top-level `Supabase/` package that composes Auth, PostgREST, Realtime, Storage, Functions, and Core into one client API.
- Child modules remain external and fixed during this work.
- The goal is to explore client-surface, lifecycle, and server-integration design without expanding into a full child-module rewrite.

## Working split

- This repository is for top-level client design and implementation.
- Comparison, reproduction, and upstream child-module work can continue separately in the lab and in the community repositories.
- Bugs found below the top-level client boundary are handled upstream rather than patched locally here.

## Scope locks

- Reimplement the stateful client, stateless client, realtime-aware table wrapper, endpoint derivation, auth propagation, and session lifecycle orchestration.
- Keep child-module behavior constant by consuming pinned submodules.
- Treat DI, readiness, observability, and lifecycle control as first-class SDK concerns.
- Target `net8.0` and `net10.0`.

## Design priorities

- Typed lifecycle transitions instead of order-dependent runtime setup.
- Replayable auth-state observation instead of imperative fan-out from the orchestrator.
- Standard .NET observability through `ILogger<T>`, `ActivitySource`, and `IMeterFactory`.
- Deterministic endpoint derivation with structured URL handling and tests.

## Non-goals

- Rewriting Auth, PostgREST, Realtime, Storage, Functions, or Core internals.
- Carrying local patches to child modules in this repository.
- Publishing a NuGet package as part of the current phase.
- Expanding scope beyond the top-level client boundary.

## Open technical decisions

- `Newtonsoft.Json` vs `System.Text.Json` at the top-level client layer.
- Final integration-test mix between local Supabase stack coverage and mocked transport coverage.
