# orange-dot Supabase C# SDK

[![Build And Test](https://github.com/orange-dot/orange-dot-supabase-sdk/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/orange-dot/orange-dot-supabase-sdk/actions/workflows/build-and-test.yml)

Targets: `net8.0`, `net10.0`

`orange-dot-supabase-sdk` is a source-first prototype for the orchestration layer of a Supabase C# client. It keeps the upstream child modules pinned and unchanged, and focuses on the top-level client surface where lifecycle, DI, readiness, auth propagation, URL derivation, observability, table convenience, and runnable usage samples are defined.

Status: prototype. Source-first repo with runnable server-side samples. It does not yet ship as a NuGet package.

## Repository Focus

- typed lifecycle with `Configure -> LoadPersistedSessionAsync -> InitializeAsync`
- auth-state observation and child-client bindings
- URL derivation for hosted and self-hosted deployments
- server-side DI helpers and stateless client factories
- runnable ASP.NET Core samples and local integration coverage

## What's implemented

- `SupabaseClient` with typed lifecycle transitions and child-client accessors: `Auth`, `Postgrest`, `Realtime`, `Storage`, `Functions`
- `ISupabaseClient` plus `services.AddSupabaseHosted(...)` and readiness gating through `Task Ready`
- `ISupabaseStatelessClientFactory` plus `services.AddSupabaseServer(...)` for server-side fresh stateless clients
- `SupabaseStatelessClient` for one-shot and non-DI usage
- `samples/ServerMinimalApi/` as the minimal ASP.NET Core sample for the server-side DI path
- `samples/ResearchWorkspaceApi/` as the fuller research-workspace sample covering a backend workflow built on the SDK, plus an embedded browser cockpit, Swagger/OpenAPI docs, RLS, storage, functions, and realtime
- `Table<T>()` and `ISupabaseTable<T>` for PostgREST query chaining plus realtime `.On(...)`
- `IAuthStateObserver` and auth bindings for header-based clients and realtime
- `SupabaseUrls` derivation for hosted and self-hosted deployments
- Standard observability hooks and typed orchestration-layer exceptions

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
dotnet build OrangeDot.Supabase.sln --configuration Release
dotnet test tests/OrangeDot.Supabase.Tests/OrangeDot.Supabase.Tests.csproj --configuration Release
```

The library project references the pinned child projects directly from `modules/`.

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

Those fixtures support both smoke checks and the research-workspace sample scenario.
The `ResearchWorkspace` live slice now covers both direct SDK/RLS behavior and an authenticated sample-API HTTP flow end to end.

## Samples

`samples/ResearchWorkspaceApi` is intentionally split into two layers:

- SDK-backed backend: the ASP.NET sample uses `AddSupabaseServer(...)`, `ISupabaseStatelessClientFactory`, and the Orange Dot orchestration layer for delegated PostgREST, Storage, Functions, and Realtime behavior.
- Thin browser transport: the embedded cockpit is static HTML/CSS/JavaScript that talks to the sample API with `fetch`, and its signup/login helper exchanges credentials against local Supabase Auth over HTTP using the publishable key.

The browser shell stays lightweight and does not introduce a separate frontend SDK wrapper.

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

- [Prototype scope / decision log](docs/decision-log.md)
- [Specification notes](spec/README.md)
- [Minimal server sample](samples/ServerMinimalApi/README.md)
- [Research workspace sample with browser cockpit and Swagger](samples/ResearchWorkspaceApi/README.md)

## What This Repo Is Not

- not a NuGet package
- not a full rewrite of the child modules under `modules/`
- not a production-hardened SDK
- not a formal proof of the runtime implementation

## Current Limitations

- Source-first repo only; no NuGet publishing flow yet
- sample apps are runnable but still intentionally educational rather than production-hardened
- Child modules under `modules/` are pinned upstream dependencies and are not patched locally
- Stateless server factory calls currently allocate fresh underlying HTTP clients; `IHttpClientFactory` integration is future work
- Storage server-path behavior still depends on upstream module internals around helper initialization order
