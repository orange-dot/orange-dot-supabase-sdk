# Specification Notes

Purpose: hold supplemental model and scenario artifacts for the orchestration layer.

This directory is the source of truth for:

- invariant IDs and their canonical wording
- authority ownership at the orchestration boundary
- lifecycle and auth-state machines
- one TLA+ model for auth ordering and propagation properties
- JSON scenario inputs used by the replay-model tests under `tests/OrangeDot.Supabase.Tests/Spec/`

## Contents

- [`invariants.md`](invariants.md) — canonical invariant list referenced by all other spec artifacts
- [`authority-model.md`](authority-model.md) — authority ownership, allowed write paths, forbidden shortcuts
- [`state-machines/lifecycle.md`](state-machines/lifecycle.md) — lifecycle states, transitions, invariants
- [`state-machines/auth-state.md`](state-machines/auth-state.md) — auth-state states, transitions, invariants
- [`state-machines/bindings.md`](state-machines/bindings.md) — child-binding projection rules
- [`tla/AuthPropagation.tla`](tla/AuthPropagation.tla) + [`tla/AuthPropagation.cfg`](tla/AuthPropagation.cfg) — auth propagation model and TLC config
- [`tla/README.md`](tla/README.md) — TLC run instructions and recorded verification result
- [`test-vectors/README.md`](test-vectors/README.md) — canonical JSON shape and domain layout for replay scenarios

## Scope

These artifacts are for the orchestration layer only.

- They do not model child-module internals.
- They do not prove the full .NET implementation.
- The current vector tests replay the shared auth/lifecycle state-machine layer and URL derivation logic. They do not execute the production runtime directly.
- They are intended to keep the model/scenario layer thin and reviewable.

## Intended use

1. Keep invariants centralized so TLA, vectors, and tests reference the same rule IDs.
2. Keep the recorded auth and lifecycle scenarios stable as test inputs.
3. Use TLA+ for a narrow set of auth-ordering properties where a small model is still useful.

## TLC Note

`AuthPropagation.tla` is the only TLA+ model kept in this repository. See [`tla/README.md`](tla/README.md) for the required TLC CI check and local rerun instructions.
