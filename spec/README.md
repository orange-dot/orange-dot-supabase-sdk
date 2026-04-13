# Specification Notes

Supplemental implementation notes for auth/lifecycle semantics, test vectors, and the small TLA models used by this repo.

This directory collects:

- invariant IDs used by tests and spec notes
- authority ownership notes for lifecycle and auth handling
- lifecycle and auth-state machine descriptions
- TLA+ models for auth propagation and hosted lifecycle ordering properties
- JSON scenario inputs used by the replay-model tests under `tests/OrangeDot.Supabase.Tests/Spec/`

## Contents

- [`invariants.md`](invariants.md) — invariant list referenced by the other spec artifacts
- [`authority-model.md`](authority-model.md) — authority ownership, allowed write paths, forbidden shortcuts
- [`state-machines/lifecycle.md`](state-machines/lifecycle.md) — lifecycle states, transitions, invariants
- [`state-machines/auth-state.md`](state-machines/auth-state.md) — auth-state states, transitions, invariants
- [`state-machines/bindings.md`](state-machines/bindings.md) — child-binding projection rules
- [`tla/AuthPropagation.tla`](tla/AuthPropagation.tla) + [`tla/AuthPropagation.cfg`](tla/AuthPropagation.cfg) — auth propagation model and TLC config
- [`tla/HostedLifecycle.tla`](tla/HostedLifecycle.tla) + [`tla/HostedLifecycle.cfg`](tla/HostedLifecycle.cfg) — hosted startup and shell-gate lifecycle model and TLC config
- [`tla/README.md`](tla/README.md) — TLC run instructions and recorded verification result
- [`test-vectors/README.md`](test-vectors/README.md) — canonical JSON shape and domain layout for replay scenarios

## Scope

These artifacts are supplemental implementation material.

- They do not model child-module internals.
- They do not replace runtime tests or local integration tests.
- The current vector tests replay the shared auth/lifecycle state-machine layer and URL derivation logic. They do not execute the production runtime directly.
- The auth and hosted-lifecycle translator tests map runtime traces into the small model vocabularies used by the repo.
- Selected local integration tests run the same translators against live auth and hosted-startup flows on the local Supabase stack.

## Intended use

1. Keep invariants centralized so TLA, vectors, and tests reference the same rule IDs.
2. Keep the recorded auth and lifecycle scenarios stable as test inputs.
3. Use TLA+ for a narrow set of auth and hosted-startup ordering checks where a small model is still useful.
4. Keep the runtime-trace bridge explicit so the model vocabulary and runtime vocabulary stay aligned.

## TLC Note

`AuthPropagation.tla` and `HostedLifecycle.tla` are the TLA+ models kept in this repository. See [`tla/README.md`](tla/README.md) for the required TLC CI checks and local rerun instructions.
