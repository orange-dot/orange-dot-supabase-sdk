# Unity Concepts

This note is the shared reference for Unity work on the `dev` branch.

It is not a full Unity manual. It is the high-signal subset that matters for this repo's Unity-first line and for portable SDK work in the child modules.

## What Matters Here

For this repo, Unity work sits at the intersection of:

- C# language and .NET API compatibility
- Unity's engine-driven lifecycle and main-thread rules
- allocation-sensitive runtime behavior
- package structure through UPM, asmdefs, and `Samples~`
- clear boundaries between Unity code and the server-side SDK line

The current Unity slice in this repo is intentionally narrow:

- auth
- session persistence
- typed PostgREST data access
- one runnable sample

## Runtime Surface

Treat Unity-facing code conservatively.

- Prefer C# syntax that stays within Unity's documented scripting surface.
- Avoid relying on the latest .NET-only conveniences unless the package target and Unity version are explicitly validated.
- Be careful with reflection-heavy or dynamic code on AOT targets.
- Favor explicit, portable code over clever framework-specific tricks.

For this repo, that means:

- keep Unity package code separate from `Microsoft.Extensions.*`
- keep package code free of server-hosted startup assumptions
- prefer simple constructors and explicit options objects

## Main Thread and Async

Assume `UnityEngine` APIs are main-thread only unless Unity documentation for that API clearly says otherwise.

When using async code:

- do background work only for real I/O or heavy computation
- switch back before touching Unity objects
- do not build a threading story around assumptions that editor behavior equals player behavior

Use `Task` and async carefully. If a Unity-specific async primitive is introduced later, verify its semantics before adopting it broadly.

For this repo's Unity SDK line:

- auth and HTTP composition can stay plain async
- sample UI code should not hide thread hops
- session restore and sign-in should remain straightforward and debuggable

## Lifecycle and Initialization

Unity object lifecycle is not the same as server startup.

Important habits:

- use `Awake` for intra-object setup
- use `Start` for work that depends on scene state being ready
- do not assume cross-object initialization order unless you enforce it

For samples:

- prefer one scene and one bootstrap path
- make initialization explicit
- surface failure as visible status text, not silent background behavior

## Serialization and Data Modeling

Unity serialization is selective and opinionated.

Use:

- plain serializable fields for simple inspector configuration
- `ScriptableObject` when shared asset-backed data is the right fit
- explicit DTO/model classes for API payloads and table records

Avoid:

- assuming every C# shape serializes cleanly in the inspector
- overusing reference-heavy serialization features unless they are needed
- mixing runtime API models with editor-only authoring concerns

For this repo:

- `SupabaseUnityOptions` should stay small and explicit
- sample controller state should stay simple and visible
- auth/session persistence should use file-backed storage by default

## Performance Rules

Unity performance work is often less about raw CPU and more about avoiding unnecessary managed allocations and unstable frame behavior.

Default rules:

- avoid LINQ in frame loops
- avoid accidental boxing
- avoid closure-heavy hot paths
- cache or reuse where repeated allocations would happen every frame
- prefer obvious loops over elegant abstractions in hot code

For this repo, samples can be simple, but they should still avoid sloppy frame-time behavior if a cleaner version is easy.

## Coroutines, Tasks, and Update Loops

Use the simplest tool that fits:

- use plain methods for immediate synchronous setup
- use async `Task` for HTTP/auth work
- use coroutines only when a real frame-based wait flow is more natural

Do not turn every flow into a coroutine just because it is Unity.

For the current auth + data slice, plain async code is a better fit than coroutine-heavy control flow.

## Jobs, Burst, and Threads

Do not reach for Jobs or Burst unless the problem is actually compute-heavy.

This repo's Unity work is currently SDK/client integration work, not simulation or rendering optimization work. That means:

- jobs are not the default answer
- ad hoc threads are not the default answer
- correctness and portability matter more than speculative micro-optimization

If later work introduces heavy data transforms or large client-side processing, profile first and then decide whether Jobs/Burst are justified.

## Package Structure

Unity package work in this repo should respect the current split:

- `modules/core-csharp/Core/` for shared portable core
- `modules/gotrue-csharp/Gotrue/` for auth
- `modules/postgrest-csharp/Postgrest/` for data
- `unity/OrangeDot.Supabase.Unity/` for Unity-facing composition

Rules:

- keep asmdefs focused
- keep `package.json` dependency edges explicit
- use `Samples~` for importable examples
- do not push Unity-specific glue into the server-side root package

## Samples

Good Unity samples in this repo should be:

- small
- importable
- honest about setup
- explicit about required SQL/RLS state
- easy to run without a lot of scene architecture

The current bar is:

- configure project URL and anon key
- restore session if present
- sign in
- insert one row
- load rows
- sign out

That is enough for a real first slice.

## Testing and Validation

There are two kinds of validation here:

1. .NET validation for package logic
2. Unity validation for editor/import/runtime behavior

Before calling Unity work done in this repo, prefer:

```bash
dotnet build unity/OrangeDot.Supabase.Unity/Runtime/OrangeDot.Supabase.Unity.Runtime.csproj -c Release -p:NuGetAudit=false
dotnet test unity/OrangeDot.Supabase.Unity/Tests/OrangeDot.Supabase.Unity.Tests.csproj -c Release -p:NuGetAudit=false
```

If child modules were touched, also verify the root line still holds:

```bash
dotnet build OrangeDot.Supabase.sln --configuration Release -m:1 -p:NuGetAudit=false
dotnet test tests/OrangeDot.Supabase.Tests/OrangeDot.Supabase.Tests.csproj --configuration Release -p:NuGetAudit=false
```

.NET success is necessary, but not the same as a real Unity import/run check. Keep that distinction explicit.

## Platform Notes

Keep these in mind when making portability claims:

- mobile and Web builds are more sensitive to allocations and threading assumptions
- AOT targets punish reflection-heavy or dynamic runtime tricks
- editor success is not enough for claiming broad platform support

For now, keep Unity claims narrow and based on what the branch actually validates.

## Security and Networking

If this Unity line later grows beyond auth + data, keep a few basics in mind:

- never treat client input as trusted
- do not use unsafe legacy serializers for untrusted data
- be explicit about token storage and auth state transitions
- validate any future realtime or multiplayer claims separately

## Repo Guardrail

The root `src/OrangeDot.Supabase/` package is still the server-side line.

The Unity-first work on this branch is a separate product line in incubation. Build it like a real Unity package, not like a disguised ASP.NET Core client.
