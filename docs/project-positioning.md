# Project Positioning

Date: 2026-04-05
Purpose: Explain the public scope and positioning of this repository.

## Core statement

This repository targets the orchestration layer of the Supabase C# SDK: the small top-level surface that composes Auth, PostgREST, Realtime, Storage, Functions, and Core into a single client API.

## Technical scope

- Reimplements the orchestration surface in `Supabase/`, including the stateful client, stateless client, realtime-aware table wrapper, endpoint derivation, auth propagation, and session lifecycle.
- Reuses the existing community child modules unchanged, consumed as submodules at fixed commits.
- Isolates DI, readiness, observability, and lifecycle decisions at the composition boundary so they can be tested independently of child-module internals.
- Keeps child-module fixes separate: issues discovered below the orchestration boundary should be handled upstream in the relevant module.

## How to read this repository

- Start with [`architecture-contrasts.md`](architecture-contrasts.md) for the core design decisions.
- Read [`implementation-plan.md`](implementation-plan.md) for the execution order and cut list.
- Read [`decision-log.md`](decision-log.md) for the prototype scope and working split.
