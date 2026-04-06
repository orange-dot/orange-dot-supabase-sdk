# TLA+ Verification Notes

This directory holds the TLA+ model used for auth propagation ordering and replay behavior.

## Files

- [`AuthPropagation.tla`](AuthPropagation.tla) — model
- [`AuthPropagation.cfg`](AuthPropagation.cfg) — TLC configuration

## How to run

From the repository root:

```bash
./scripts/run-auth-tlc.sh
```

The script downloads `tla2tools.jar` into `~/tools/tla/` if needed and runs TLC against `AuthPropagation.tla` with `AuthPropagation.cfg`.

## Verified run

Last recorded successful TLC run:

- Date: 2026-04-06
- Command: `./scripts/run-auth-tlc.sh`
- Result: `Model checking completed. No error has been found.`
- States generated: `62657`
- Distinct states: `8864`
- Search depth: `14`
- Runtime: about `9s`

## What this run checks

From [`AuthPropagation.cfg`](AuthPropagation.cfg):

- `TypeOK`
- `SignedOutClearsBindings`
- `ProjectedVersionNeverLeads`
- `RefreshingUsesFutureVersion`
- `AuthenticatedBindingsSettleOrAuthChanges`
- `SignOutEventuallyQuiescesPendingRefresh`

## Maintenance rule

If `AuthPropagation.tla` or `AuthPropagation.cfg` changes, rerun TLC and update the recorded run summary in this file.
