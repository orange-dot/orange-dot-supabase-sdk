# Orange Dot Supabase Unity

`OrangeDot.Supabase.Unity` is a Unity-first prototype package built on top of the portable `Supabase.Core`, `Supabase.Gotrue`, and `Supabase.Postgrest` child modules in this repo.

This package is intentionally narrow in `v0.1`:

- Auth session restore and email/password sign-in
- File-backed session persistence for Unity apps
- Auth-aware PostgREST access for typed data queries
- One importable sample that shows login, insert, query, and sign-out

This package does **not** claim broad Unity coverage for the whole repo. `Realtime`, `Storage`, `Functions`, and the root server-side `OrangeDot.Supabase` package are outside this first Unity slice.

## Package Setup

In a Unity project, add these local packages from disk:

- `modules/core-csharp/Core/package.json`
- `modules/gotrue-csharp/Gotrue/package.json`
- `modules/postgrest-csharp/Postgrest/package.json`
- `unity/OrangeDot.Supabase.Unity/package.json`

Also install:

- `com.unity.nuget.newtonsoft-json` `3.2.1`

## Runtime Surface

The package exposes:

- `SupabaseUnityOptions`
- `SupabaseUnityUrls`
- `SupabaseUnityClient`
- `UnitySessionPersistence`

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
- a short sample README with SQL for the demo table and RLS policies

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
