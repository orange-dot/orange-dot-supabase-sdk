# Verification Spec

Purpose: hold the verification-oriented artifacts for the orchestration layer.

This directory is the source of truth for:

- invariant IDs and their canonical wording
- authority ownership at the orchestration boundary
- lifecycle and auth-state machines
- one TLA+ model for auth ordering and propagation properties
- test-vector inputs for future replay and executable-spec work

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
- They do not attempt full theorem-prover coverage of the .NET implementation.
- They are intended to keep the spec layer thin, reviewable, and directly consumable by tests and interviews.

## Intended use

1. Lock the authority model and state machines before implementation expands.
2. Keep invariants centralized so TLA, vectors, and future tests reference the same rule IDs.
3. Keep pure reducers and runtime orchestration aligned with the same transitions.
4. Use TLA+ only where it adds signal beyond the type system: auth replay, sign-out clearing, and refresh ordering.
5. Keep replay vectors focused on behavioral scenarios that map directly into future tests.

## TLC Note

`AuthPropagation.tla` is the only TLA+ artifact kept in this repository because it adds real signal beyond the typed API surface. See [`tla/README.md`](tla/README.md) for the recorded successful TLC run and rerun rule.
