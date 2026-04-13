# Unity SmokeHost

`SmokeHost` is the local Unity validation project for the Unity-first Supabase line on this branch.

It is intentionally small:

- it references staged local UPM packages under `LocalPackages/`
- it syncs the current `AuthAndData` sample into `Assets/Samples/`
- it is used to verify that the package graph imports and compiles in a real Unity Editor
- it is not part of the server-side `.NET` story

## Workflow

1. Prepare staged packages:

```bash
bash unity/scripts/prepare-smoke-host.sh
```

2. Run a headless compile/import pass:

```bash
bash unity/scripts/run-smoke-host-batch.sh
```

3. Open the project in Unity Hub or the Editor if you want to import samples and test interactively.

## Why staged packages?

The real package roots under `modules/*` and `unity/OrangeDot.Supabase.Unity` also contain `.NET` build artifacts such as `obj/` and `bin/`. Unity local file packages will try to compile generated `*.cs` files inside those folders, which leads to duplicate assembly attribute errors.

`prepare-smoke-host.sh` copies only Unity-relevant package content into `unity/SmokeHost/LocalPackages/` and excludes those generated artifacts.

The same script also copies the current package sample into `unity/SmokeHost/Assets/Samples/AuthAndData/` so sample code is part of the Unity compile/import pass.
