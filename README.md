# Orange Dot Supabase C# SDK

[![Build And Test](https://github.com/orange-dot/orange-dot-supabase-sdk/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/orange-dot/orange-dot-supabase-sdk/actions/workflows/build-and-test.yml)

Targets: `net8.0`, `net10.0`

`orange-dot-supabase-sdk` is a C# SDK repo for Supabase focused on idiomatic .NET integration, typed client setup, server-side bearer-token delegation, and runnable ASP.NET Core examples.

Current state:

- builds from source with pinned child modules under `modules/`
- runnable server-side samples and local integration coverage
- not yet published as a NuGet package

## What this repo shows

- a top-level C# client surface across Auth, PostgREST, Realtime, Storage, and Functions
- ASP.NET Core integration through hosted startup and stateless server-side factories
- typed lifecycle with `Configure -> LoadPersistedSessionAsync -> InitializeAsync`
- delegated bearer-token access for per-user server operations
- URL derivation for hosted and self-hosted deployments
- local integration tests and runnable samples for common .NET usage paths

## What's implemented

- `SupabaseClient` with typed lifecycle transitions and child-client accessors: `Auth`, `Postgrest`, `Realtime`, `Storage`, `Functions`
- `ISupabaseClient` plus `services.AddSupabaseHosted(...)` and readiness gating through `Task Ready`
- `ISupabaseStatelessClientFactory` plus `services.AddSupabaseServer(...)` for fresh per-operation server clients
- `SupabaseStatelessClient` for one-shot and non-DI usage
- `Table<T>()` and `ISupabaseTable<T>` for PostgREST query chaining plus realtime `.On(...)`
- auth-state observation and binding projection for header-based clients and realtime
- `SupabaseUrls` derivation for hosted and self-hosted deployments
- standard .NET observability hooks and typed SDK exceptions
- runnable ASP.NET Core samples:
  - `samples/ServerMinimalApi/` for the minimal server-side DI path
  - `samples/ResearchWorkspaceApi/` for a broader sample covering delegated auth, RLS-backed CRUD, storage, functions, realtime, and Swagger/OpenAPI

## Prerequisites

This repo consumes pinned upstream child modules as git submodules.

```bash
git clone https://github.com/orange-dot/orange-dot-supabase-sdk.git
cd orange-dot-supabase-sdk
git submodule update --init --recursive
```

For the default multi-target test commands, the local machine needs:

- .NET 10 SDK
- `Microsoft.NETCore.App` 8.0.x runtime
- `Microsoft.NETCore.App` 10.0.x runtime

Check the installed runtimes with:

```bash
dotnet --list-runtimes
```

If `Microsoft.NETCore.App 8.0.x` is missing, install the .NET 8 runtime or SDK before running the default test commands. The GitHub Actions workflow installs .NET from `global.json` and also installs `8.0.x` explicitly for the same reason.

## Build And Unit Tests

```bash
dotnet build OrangeDot.Supabase.sln --configuration Release -m:1
dotnet test tests/OrangeDot.Supabase.Tests/OrangeDot.Supabase.Tests.csproj --configuration Release
```

The solution build runs with `-m:1` because the library references the pinned child projects directly from `modules/`, and the serial solution build path is the stable local build path for this repo.

## Integration Tests

The repo also carries a minimal local `supabase/` setup and an opt-in integration test project.

```bash
supabase start
ORANGEDOT_SUPABASE_RUN_INTEGRATION=1 dotnet test tests/OrangeDot.Supabase.IntegrationTests/OrangeDot.Supabase.IntegrationTests.csproj --configuration Release -f net10.0
```

Without `ORANGEDOT_SUPABASE_RUN_INTEGRATION=1`, integration tests are skipped by default so normal unit-test runs stay green without a local Supabase stack.
GitHub Actions runs the live integration suite on Ubuntu. Windows and macOS remain unit-test only.
CI currently pins Supabase CLI `2.84.2` for reproducible integration runs.

The local stack now includes repo-managed storage and edge-function fixtures:
- storage bucket: `integration-public`
- storage bucket: `research-artifacts`
- edge function: `orangedot-integration-smoke`
- edge function: `orangedot-integration-failure`
- edge function: `research-promote-baseline`

Those fixtures support both smoke checks and the broader sample API flow.

## Samples

- `samples/ServerMinimalApi/` is the quickest way to see `AddSupabaseServer(...)`, anon access, and bearer-token delegation in a minimal ASP.NET Core app.
- `samples/ResearchWorkspaceApi/` is the broader sample API. It shows the SDK in a larger server-side flow with delegated auth, RLS-backed data access, storage, functions, realtime, Swagger/OpenAPI, and a lightweight browser cockpit for exercising the API.

## Long-Lived Client

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

## Hosted Client In ASP.NET Core

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

## One-Shot Stateless Client

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
- `CreateService()` uses the configured privileged key as the child-client `apikey`; legacy JWT service-role keys are also mirrored into `Authorization` for local/self-hosted compatibility
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

- [Minimal server sample](samples/ServerMinimalApi/README.md)
- [Broader sample API with Swagger and browser cockpit](samples/ResearchWorkspaceApi/README.md)
- [Specification notes](spec/README.md)

## What This Repo Is Not

- not published on NuGet
- not a rewrite of the pinned child modules under `modules/`
- not a production-hardened SDK

## Current Limitations

- build-from-source repo only; no NuGet publishing flow yet
- child modules under `modules/` are pinned upstream dependencies and are not patched locally
- Stateless server factory calls currently allocate fresh underlying HTTP clients; `IHttpClientFactory` integration is future work
- Storage server-path behavior still depends on upstream module internals around helper initialization order
