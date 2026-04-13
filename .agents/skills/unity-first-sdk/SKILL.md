---
name: unity-first-sdk
description: "Use when working on the Unity-first prototype line in this repo: Unity package structure, asmdefs, UPM package.json files, Unity runtime code, portable child modules for Unity, sample scenes/controllers, session persistence, or general Unity implementation decisions. Trigger for tasks under unity/, Unity package work in modules/, or when Unity architecture/performance/runtime constraints matter."
---

# Unity-First SDK

Use this skill for Unity work in this repo.

## Scope

This repo has two separate lines:

- `src/OrangeDot.Supabase/` is the server-side .NET SDK line.
- `unity/` plus selected child modules in `modules/` form the Unity-first prototype line.

Do not blur them together. Do not try to make the root server package look Unity-ready unless the task explicitly asks for that.

## Repo Shape

Start with:

- [unity/README.md](../../../unity/README.md)
- [unity/OrangeDot.Supabase.Unity/README.md](../../../unity/OrangeDot.Supabase.Unity/README.md)

Current Unity package flow on this branch:

- `modules/core-csharp/Core/`
- `modules/gotrue-csharp/Gotrue/`
- `modules/postgrest-csharp/Postgrest/`
- `unity/OrangeDot.Supabase.Unity/`

The current committed Unity slice is `Auth + Data`.

## Reference Material

General Unity concepts live in:

- [unity/references/unity-concepts.md](../../../unity/references/unity-concepts.md)

Do not load the whole file by default. Read only the sections that matter:

- `Runtime and language surface area` for C# / API compatibility decisions
- `Async/await in Unity` for threading and `Awaitable` concerns
- `MonoBehaviour lifecycle`, `Coroutines`, `ScriptableObjects`, and `Serialization` for Unity behavior and scene/sample code
- `Profiling toolkit`, `GC management`, and `Jobs system and Burst` for performance-sensitive work
- `Testing and QA`, `Debugging and observability`, and `Platform-specific considerations` for validation and portability decisions

## Repo Rules

- Keep Unity work in the Unity package line. Do not add `Microsoft.Extensions.*`, hosted startup, or DI-centric server patterns to the Unity package.
- Keep Unity-facing code conservative and portable. Prefer C# features that fit Unity's documented runtime/language surface.
- Treat `UnityEngine` APIs as main-thread only unless Unity documentation for that API clearly says otherwise.
- Avoid hidden allocations in hot paths:
  - avoid LINQ in frame loops
  - avoid unnecessary closures in per-frame callbacks
  - avoid accidental boxing in frequently executed code
- Prefer file-backed session persistence over `PlayerPrefs` for auth/session storage unless the task explicitly wants a different tradeoff.
- Keep samples narrow and runnable. One scene, one clear flow, explicit SQL/setup notes.
- Do not claim `Realtime`, `Storage`, or `Functions` Unity support until the branch actually validates them.

## Working Pattern

1. Confirm whether the task belongs to the Unity line, a child module portability fix, or sample/package work.
2. Read only the relevant section from `unity-concepts.md`.
3. Preserve the current product split:
   - portable child modules
   - Unity composition package
   - server-side root package
4. If touching child modules, prefer additive portability fixes over broad refactors.
5. If touching samples, optimize for real setup clarity, not visual complexity.
6. Verify both Unity-line and root-line stability before closing the task.

## Validation

For Unity package work, prefer:

```bash
dotnet build unity/OrangeDot.Supabase.Unity/Runtime/OrangeDot.Supabase.Unity.Runtime.csproj -c Release -p:NuGetAudit=false
dotnet test unity/OrangeDot.Supabase.Unity/Tests/OrangeDot.Supabase.Unity.Tests.csproj -c Release -p:NuGetAudit=false
```

If you changed child modules that the root package consumes, also run:

```bash
dotnet build OrangeDot.Supabase.sln --configuration Release -m:1 -p:NuGetAudit=false
dotnet test tests/OrangeDot.Supabase.Tests/OrangeDot.Supabase.Tests.csproj --configuration Release -p:NuGetAudit=false
```

For `gotrue-csharp` or `postgrest-csharp` portability work, add the narrowest targeted child-module build/test that proves the specific change.
