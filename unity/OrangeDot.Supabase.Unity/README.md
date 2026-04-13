# Orange Dot Supabase Unity

`OrangeDot.Supabase.Unity` is a Unity-first prototype package built on top of the portable `Supabase.Core`, `Supabase.Gotrue`, `Supabase.Postgrest`, `Supabase.Functions`, and `Supabase.Storage` child modules in this repo.

This package is intentionally narrow in `v0.1`:

- Auth session restore and email/password sign-in
- File-backed session persistence for Unity apps
- Auth-aware PostgREST access for typed data queries
- Auth-aware Edge Functions access
- Auth-aware Storage access
- One importable sample that shows login, insert, query, optional function invocation, and sign-out

This package does **not** claim broad Unity coverage for the whole repo. `Realtime` and the root server-side `OrangeDot.Supabase` package are outside this Unity slice.

## Package Setup

In a Unity project, add these local packages from disk:

- `unity/Vendor/BirdMessenger/package.json`
- `unity/Vendor/MimeMapping/package.json`
- `modules/core-csharp/Core/package.json`
- `modules/gotrue-csharp/Gotrue/package.json`
- `modules/postgrest-csharp/Postgrest/package.json`
- `modules/functions-csharp/Functions/package.json`
- `modules/storage-csharp/Storage/package.json`
- `unity/OrangeDot.Supabase.Unity/package.json`

Also install:

- `com.unity.nuget.newtonsoft-json` `3.2.1`

## Runtime Surface

The package exposes:

- `SupabaseUnityOptions`
- `SupabaseUnityUrls`
- `SupabaseUnityClient`
- `UnitySessionPersistence`

`SupabaseUnityClient` exposes:

- `Auth`
- `Postgrest`
- `Functions`
- `Storage`

Typical composition:

```csharp
using OrangeDot.Supabase.Unity;
using UnityEngine;

var client = new SupabaseUnityClient(
    new SupabaseUnityOptions
    {
        ProjectUrl = "https://YOUR_PROJECT.supabase.co",
        AnonKey = "YOUR_ANON_KEY",
        RefreshSessionOnInitialize = true
    },
    new UnitySessionPersistence(Application.persistentDataPath));

await client.InitializeAsync();
await client.SignInWithPasswordAsync("demo@example.com", "super-secret-password");
```

## Sample

Import the `AuthAndData` sample from Package Manager.

The sample contains:

- `AuthAndDataSampleController`
- `UnityTodoItem`
- a short sample README with SQL for the demo table, RLS policies, and an optional Edge Function

The sample remains runnable with just auth + data. If you also deploy the optional `unity-hello` Edge Function from the sample README, the same scene can exercise the `Functions` client after sign-in. `Storage` is exposed by the runtime package in this slice, but the first sample scene does not use it yet.

The controller uses `OnGUI`, so you can test it quickly by:

1. Creating an empty `GameObject`
2. Adding `AuthAndDataSampleController`
3. Filling in `ProjectUrl` and `AnonKey`
4. Running the scene

## Local Validation

Outside Unity, the runtime package can be built and tested with .NET:

```bash
dotnet test unity/OrangeDot.Supabase.Unity/Tests/OrangeDot.Supabase.Unity.Tests.csproj --configuration Release
```
