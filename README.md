# orange-dot Supabase C# SDK

[![Build And Test](https://github.com/orange-dot/orange-dot-supabase-sdk/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/orange-dot/orange-dot-supabase-sdk/actions/workflows/build-and-test.yml)

Targets: `net8.0`, `net10.0`

`orange-dot-supabase-sdk` is a source-first reimplementation of the orchestration layer of the Supabase C# SDK. It keeps the upstream child modules pinned and unchanged, and focuses on the top-level client surface where lifecycle, DI, readiness, auth propagation, URL derivation, observability, table convenience, and runnable usage samples are defined.

Today the repo includes a working stateful client, a DI/hosted construction path, a stateless client, a realtime-aware `Table<T>()` wrapper, a minimal ASP.NET Core server sample, typed orchestration-layer exceptions, unit-test coverage, and an opt-in local-Supabase integration test slice around the composition boundary. It does not yet ship as a NuGet package.

> Current status: usable from source, honest about its prototype scope, with an initial server-side sample included.

## Why this repo exists

- Typed lifecycle states: construction is `Configure -> LoadPersistedSessionAsync -> InitializeAsync`, so wrong-order startup bugs are not left to prose or convention.
- Replayable auth observation: child clients react to one auth-state stream instead of imperative token fan-out from the orchestrator.
- Standard .NET observability: `ILogger<T>`, `ActivitySource`, and `IMeterFactory` are first-class instead of custom debugger singletons.
- Structured URL derivation: hosted and self-hosted endpoints are derived through `Uri` handling and table-driven tests.

See [docs/architecture-contrasts.md](docs/architecture-contrasts.md) for the full design rationale.

## What's implemented

- `SupabaseClient` with typed lifecycle transitions and child-client accessors: `Auth`, `Postgrest`, `Realtime`, `Storage`, `Functions`
- `ISupabaseClient` plus `services.AddSupabaseHosted(...)` and readiness gating through `Task Ready`
- `ISupabaseStatelessClientFactory` plus `services.AddSupabaseServer(...)` for server-side fresh stateless clients
- `SupabaseStatelessClient` for one-shot and non-DI usage
- `samples/ServerMinimalApi/` as a runnable ASP.NET Core sample for the server-side DI path
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
CI currently pins Supabase CLI `2.84.2` for reproducible integration runs.

The local stack now includes repo-managed storage and edge-function fixtures:
- storage bucket: `integration-public`
- edge function: `orangedot-integration-smoke`
- edge function: `orangedot-integration-failure`

Those fixtures are intended for integration smoke checks now and richer storage/functions scenarios in follow-up PRs.

If you use the local Homebrew-based setup from this repo and see a Supabase CLI update warning:

```bash
supabase stop --no-backup
bash scripts/install-brew-and-supabase.sh
```

The script now installs Supabase CLI if missing, or runs `brew update` plus `brew upgrade supabase/tap/supabase` if it is already installed.

## Manual Lifecycle

```csharp
using OrangeDot.Supabase;

var configured = SupabaseClient.Configure(new SupabaseOptions
{
    Url = "https://abc.supabase.co",
    PublishableKey = "your-publishable-key"
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

services.AddSupabaseHosted(options =>
{
    options.Url = "https://abc.supabase.co";
    options.PublishableKey = "your-publishable-key";
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
    PublishableKey = "your-publishable-key"
});

var session = await client.Auth.SignIn(
    "user@example.com",
    "password",
    client.AuthOptions);

var todos = await client.Postgrest.Table<Todo>().Get();
```

## Server / Stateless Factory

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrangeDot.Supabase;

var services = new ServiceCollection();

services.AddSupabaseServer(options =>
{
    options.Url = "https://abc.supabase.co";
    options.PublishableKey = "your-publishable-key";
    options.SecretKey = "your-secret-key";
});
```

```csharp
public sealed class TodoQueryHandler
{
    private readonly ISupabaseStatelessClientFactory _clients;

    public TodoQueryHandler(ISupabaseStatelessClientFactory clients)
    {
        _clients = clients;
    }

    public Task<object> GetPublicTodosAsync()
    {
        var client = _clients.CreateAnon();
        return client.Postgrest.Table<Todo>().Get();
    }

    public Task<object> GetUserTodosAsync(string accessToken)
    {
        var client = _clients.CreateForUser(accessToken);
        return client.Postgrest.Table<Todo>().Get();
    }
}
```

Server factory notes:
- each factory call creates fresh child clients and fresh underlying HTTP clients
- callers own delegated-token lifecycle and expiry handling
- `AuthOptions` stays project-level; delegated identity is carried by the factory-created child-client headers, not by `AuthOptions`
- prefer `PublishableKey` for project-level client configuration and `SecretKey` only for privileged server operations
- the current server path is correct for isolated per-operation usage, but not yet optimized for very high client churn
- Storage still inherits upstream static-helper constraints under concurrent mixed-option initialization; PR21 does not change that module behavior

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
- [Server sample](samples/ServerMinimalApi/README.md)

## Current Limitations

- Source-first repo only; no NuGet publishing flow yet
- Sample application coverage is still minimal; only the server-side Minimal API sample is included so far
- Child modules under `modules/` are pinned upstream dependencies and are not patched locally
- Stateless server factory calls currently allocate fresh underlying HTTP clients; `IHttpClientFactory` integration is future work
- Storage server-path behavior still depends on upstream module internals around helper initialization order
