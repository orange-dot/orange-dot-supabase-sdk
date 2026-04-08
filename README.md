# orange-dot Supabase C# SDK

[![Build And Test](https://github.com/orange-dot/orange-dot-supabase-sdk/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/orange-dot/orange-dot-supabase-sdk/actions/workflows/build-and-test.yml)

Targets: `net8.0`, `net10.0`

`orange-dot-supabase-sdk` is a source-first reimplementation of the orchestration layer of the Supabase C# SDK. It keeps the upstream child modules pinned and unchanged, and focuses on the top-level client surface where lifecycle, DI, readiness, auth propagation, URL derivation, observability, and table convenience are defined.

Today the repo includes a working stateful client, a DI/hosted construction path, a stateless client, a realtime-aware `Table<T>()` wrapper, typed orchestration-layer exceptions, unit-test coverage, and an opt-in local-Supabase integration test slice around the composition boundary. It does not yet ship as a NuGet package and does not yet include sample apps.

> Current status: usable from source, honest about its prototype scope, with sample-app slices still deferred.

## Why this repo exists

- Typed lifecycle states: construction is `Configure -> LoadPersistedSessionAsync -> InitializeAsync`, so wrong-order startup bugs are not left to prose or convention.
- Replayable auth observation: child clients react to one auth-state stream instead of imperative token fan-out from the orchestrator.
- Standard .NET observability: `ILogger<T>`, `ActivitySource`, and `IMeterFactory` are first-class instead of custom debugger singletons.
- Structured URL derivation: hosted and self-hosted endpoints are derived through `Uri` handling and table-driven tests.

See [docs/architecture-contrasts.md](docs/architecture-contrasts.md) for the full design rationale.

## What's implemented

- `SupabaseClient` with typed lifecycle transitions and child-client accessors: `Auth`, `Postgrest`, `Realtime`, `Storage`, `Functions`
- `ISupabaseClient` plus `services.AddSupabase(...)` and readiness gating through `Task Ready`
- `SupabaseStatelessClient` for one-shot and non-DI usage
- `Table<T>()` and `ISupabaseTable<T>` for PostgREST query chaining plus realtime `.On(...)`
- `IAuthStateObserver` and auth bindings for header-based clients and realtime
- `SupabaseUrls` derivation for hosted and self-hosted deployments
- Standard observability hooks and typed orchestration-layer exceptions

## Build From Source

This repo consumes pinned upstream child modules as git submodules. Initialize them before building:

```bash
git clone https://github.com/orange-dot/orange-dot-supabase-sdk.git
cd orange-dot-supabase-sdk
git submodule update --init --recursive
dotnet test OrangeDot.Supabase.sln --configuration Release
```

The library project references the pinned child projects directly from `modules/`.

## Integration Tests

The repo also carries a minimal local `supabase/` setup and an opt-in integration test project.

```bash
git submodule update --init --recursive
supabase start
ORANGEDOT_SUPABASE_RUN_INTEGRATION=1 dotnet test OrangeDot.Supabase.sln --configuration Release
```

Without `ORANGEDOT_SUPABASE_RUN_INTEGRATION=1`, integration tests are skipped by default so normal CI and local unit-test runs stay green without a local Supabase stack.
GitHub Actions also runs the live integration suite on Ubuntu; Windows and macOS remain unit-test only.

The local stack now includes repo-managed storage and edge-function fixtures:
- storage bucket: `integration-public`
- edge function: `orangedot-integration-smoke`
- edge function: `orangedot-integration-failure`

Those fixtures are intended for integration smoke checks now and richer storage/functions scenarios in follow-up PRs.

## Manual Lifecycle

```csharp
using OrangeDot.Supabase;

var configured = SupabaseClient.Configure(new SupabaseOptions
{
    Url = "https://abc.supabase.co",
    AnonKey = "your-anon-key"
});

var hydrated = await configured.LoadPersistedSessionAsync();
var client = await hydrated.InitializeAsync();

var todos = await client.Table<Todo>().Get();
```

## DI / Hosted Startup

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrangeDot.Supabase;

var services = new ServiceCollection();

services.AddSupabase(options =>
{
    options.Url = "https://abc.supabase.co";
    options.AnonKey = "your-anon-key";
});
```

```csharp
public sealed class TodoService
{
    private readonly ISupabaseClient _supabase;

    public TodoService(ISupabaseClient supabase)
    {
        _supabase = supabase;
    }

    public async Task<object> GetTodosAsync()
    {
        await _supabase.Ready;
        return await _supabase.Table<Todo>().Get();
    }
}
```

## Stateless Client

```csharp
using OrangeDot.Supabase;

var client = SupabaseStatelessClient.Create(new SupabaseOptions
{
    Url = "https://abc.supabase.co",
    AnonKey = "your-anon-key"
});

var session = await client.Auth.SignIn(
    "user@example.com",
    "password",
    client.AuthOptions);

var todos = await client.Postgrest.Table<Todo>().Get();
```

## Table Wrapper

`Table<T>()` keeps PostgREST query chaining on the wrapper and adds table-scoped realtime subscriptions.

```csharp
using Supabase.Realtime.PostgresChanges;

var channel = await client.Table<Todo>().On(
    PostgresChangesOptions.ListenType.Updates,
    (_, change) => Console.WriteLine(change.EventType),
    filter: "id=eq.7");
```

`Where(...)`, `Filter(...)`, and other PostgREST query operators affect HTTP queries only. Realtime filters must be passed explicitly to `.On(...)`.

## Further Reading

- [Architecture contrasts](docs/architecture-contrasts.md)
- [Prototype scope / decision log](docs/decision-log.md)
- [Project positioning](docs/project-positioning.md)
- [Verification artifacts](spec/README.md)

## Current Limitations

- Source-first repo only; no NuGet publishing flow yet
- No sample applications in the repo yet
- Child modules under `modules/` are pinned upstream dependencies and are not patched locally
