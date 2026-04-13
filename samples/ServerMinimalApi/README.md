# ServerMinimalApi

Minimal ASP.NET Core sample for `AddSupabaseServer(...)` and `ISupabaseStatelessClientFactory`.

This sample shows:

- project-level Supabase configuration from ASP.NET Core settings
- anon access for public reads
- bearer-token delegation for per-user reads
- fresh stateless SDK clients per request

## Run

```bash
supabase start
dotnet run --project samples/ServerMinimalApi/ServerMinimalApi.csproj --urls http://127.0.0.1:5000
```

The sample reads:

- `Supabase:Url`
- `Supabase:PublishableKey`
- `Supabase:SecretKey`

`appsettings.Development.json` is already pointed at the local default Supabase stack and publishable key, so the sample can run against `supabase start` without extra config.

## Endpoints

Health check:

```bash
curl http://127.0.0.1:5000/health
```

Anon query:

```bash
curl http://127.0.0.1:5000/todos/public
```

User-delegated query:

```bash
curl \
  -H "Authorization: Bearer <access-token>" \
  http://127.0.0.1:5000/todos/user
```

Each request resolves a fresh SDK client through `ISupabaseStatelessClientFactory`.
The sample queries the repo-managed `integration_todos` table from the local Supabase stack.

For `/todos/user`, supply a real Supabase access token in the standard `Authorization: Bearer ...` header.
