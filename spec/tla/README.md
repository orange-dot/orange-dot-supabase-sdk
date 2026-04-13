# TLA+ Model Notes

This directory holds the TLA+ model used for selected auth propagation ordering properties.

It is a supplemental model. It does not prove the .NET implementation end to end.

## Files

- [`AuthPropagation.tla`](AuthPropagation.tla) — model
- [`AuthPropagation.cfg`](AuthPropagation.cfg) — TLC configuration

## How to run

From the repository root:

```bash
./scripts/run-auth-tlc.sh
```

The script downloads `tla2tools.jar` into `~/tools/tla/` if needed and runs TLC against `AuthPropagation.tla` with `AuthPropagation.cfg`.

CI runs the same script with a pinned stable `tla2tools.jar` version and a runner-local tool directory.

## Recorded TLC Run

Last recorded successful TLC run:

- Date: 2026-04-13
- Command: `./scripts/run-auth-tlc.sh`
- Result: `Model checking completed. No error has been found.`
- States generated: `62657`
- Distinct states: `8864`
- Search depth: `14`
- Runtime: about `4s`

## What this run checks

From [`AuthPropagation.cfg`](AuthPropagation.cfg):

- `TypeOK`
- `SignedOutClearsBindings`
- `ProjectedVersionNeverLeads`
- `RefreshingUsesFutureVersion`
- `AuthenticatedBindingsSettleOrAuthChanges`
- `SignOutEventuallyQuiescesPendingRefresh`

## CI Rule

TLC is a required CI check on every pull request and push to `main`.

## Maintenance rule

If `AuthPropagation.tla`, `AuthPropagation.cfg`, or the shared auth conformance layer changes, CI should continue to pass and the recorded run summary in this file should stay representative of a recent successful run.
