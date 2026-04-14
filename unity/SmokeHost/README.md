# Unity SmokeHost

`SmokeHost` is the local Unity validation project for the Unity-first Supabase line on this branch.

It is intentionally small:

- it references staged local UPM packages under `LocalPackages/`
- it syncs the current `AuthAndData` sample into `Assets/Samples/`
- it carries a tracked local demo scene under `Assets/Scenes/`
- it is used to verify that the package graph imports and compiles in a real Unity Editor
- it is not part of the server-side `.NET` story

## Workflow

1. Prepare staged packages:

```bash
bash unity/scripts/prepare-smoke-host.sh
```

That command now does three things together:

- starts the local Supabase stack if needed
- ensures the demo table, storage bucket, policies, and demo user exist
- stages the local Unity packages and syncs the sample into `Assets/Samples/`

2. Run a headless compile/import pass:

```bash
bash unity/scripts/run-smoke-host-batch.sh
```

3. Open the project in Unity Hub or the Editor if you want to import samples and test interactively.

When the project opens with the default untitled scene, `SmokeHost` will switch to `Assets/Scenes/LocalSupabaseAuthAndData.unity` automatically for the current editor session.

That scene is preconfigured for the local Supabase stack used in this repo:

- `ProjectUrl` -> `http://127.0.0.1:54321`
- `AnonKey` -> repo local anon key
- `Email` -> `unity@example.com`
- `Password` -> `password123`
- `StorageBucket` -> `unity-sample`
- `FunctionName` -> `orangedot-integration-smoke`

Use `Orange Dot > Unity SmokeHost > Rebuild Local Demo Scene` if you want to regenerate the tracked demo scene from the current sample defaults.

The backend bootstrap script is [unity/scripts/bootstrap-local-smokehost-backend.sh](/home/dev/orange-dot-supabase-sdk/unity/scripts/bootstrap-local-smokehost-backend.sh). Set `UNITY_SMOKE_BOOTSTRAP_BACKEND=0` if you want `prepare-smoke-host.sh` to skip backend setup and only sync Unity assets.

## Why staged packages?

The real package roots under `modules/*` and `unity/OrangeDot.Supabase.Unity` also contain `.NET` build artifacts such as `obj/` and `bin/`. Unity local file packages will try to compile generated `*.cs` files inside those folders, which leads to duplicate assembly attribute errors.

`prepare-smoke-host.sh` copies only Unity-relevant package content into `unity/SmokeHost/LocalPackages/` and excludes those generated artifacts.

The same script also copies the current package sample into `unity/SmokeHost/Assets/Samples/AuthAndData/` so sample code is part of the Unity compile/import pass and the tracked demo scene can reference the same runtime-facing sample controller.
