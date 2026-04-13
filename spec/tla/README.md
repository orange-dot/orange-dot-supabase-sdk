# TLA+ Model Notes

This directory holds two small TLA+ models used to maintain the trickier auth and hosted-startup semantics in this repo:

- auth propagation ordering
- hosted startup plus shell readiness gating

They complement unit and integration tests. They do not prove the full SDK end to end.

## Files

- [`AuthPropagation.tla`](AuthPropagation.tla) + [`AuthPropagation.cfg`](AuthPropagation.cfg) — auth propagation model and TLC config
- [`HostedLifecycle.tla`](HostedLifecycle.tla) + [`HostedLifecycle.cfg`](HostedLifecycle.cfg) — hosted startup and shell-gate lifecycle model and TLC config

## How the models connect to the code

The checked path in this repository is:

1. runtime code emits semantic trace events
2. trace-to-model translators map those runtime traces to model actions
3. unit tests verify the translators against real runtime traces
4. TLC checks the bounded auth and lifecycle models in CI
5. selected local integration tests run the same translators against live runtime traces from the local Supabase stack

This is a narrow maintenance path for two state-heavy areas, not end-to-end proof of the full runtime or the full live local stack.

## How to run

From the repository root:

```bash
bash scripts/run-auth-tlc.sh
bash scripts/run-lifecycle-tlc.sh
```

The wrappers call the shared runner:

```bash
bash scripts/run-tlc.sh AuthPropagation
bash scripts/run-tlc.sh HostedLifecycle
```

The shared runner downloads `tla2tools.jar` into `~/tools/tla/` if needed. CI runs the same checks with a pinned `tla2tools.jar` version and a runner-local tool directory.

## Recorded TLC Runs

### AuthPropagation

- Date: `2026-04-13`
- Command: `bash scripts/run-auth-tlc.sh`
- Result: `Model checking completed. No error has been found.`
- States generated: `96353`
- Distinct states: `12928`
- Search depth: `13`
- Runtime: about `4s`

Checks from [`AuthPropagation.cfg`](AuthPropagation.cfg):

- `TypeOK`
- `SignedOutClearsBindings`
- `ProjectedVersionNeverLeads`
- `RefreshingUsesFutureVersion`
- `SignedOutHasNoPendingRefresh`
- `AuthenticatedBindingsSettleOrAuthChanges`

### HostedLifecycle

- Date: `2026-04-13`
- Command: `bash scripts/run-lifecycle-tlc.sh`
- Result: `Model checking completed. No error has been found.`
- States generated: `478`
- Distinct states: `63`
- Search depth: `8`
- Runtime: under `1s`

Checks from [`HostedLifecycle.cfg`](HostedLifecycle.cfg):

- `TypeOK`
- `DeniedNeverExceedsAttempts`
- `AllowedNeverExceedsAttempts`
- `AllowedCallsRequireReady`
- `ReadyImpliesPublished`
- `PublicationSkipRequiresStopRequested`
- `PublicationSkipKeepsLifecycleCanceled`
- `CanceledOrFaultedNeverReady`

## CI Rule

Both TLC checks are required on every pull request and push to `main`.

## Maintenance rule

If either `.tla` / `.cfg` pair, the shared auth or lifecycle conformance layer, or either trace-to-model translator changes, CI should continue to pass and the recorded run summaries in this file should stay representative of a recent successful run.
