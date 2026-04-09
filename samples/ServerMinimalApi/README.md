# ServerMinimalApi

Minimal ASP.NET Core sample for the server-side `AddSupabaseServer(...)` flow.

## Run

```bash
supabase start
dotnet run --project samples/ServerMinimalApi/ServerMinimalApi.csproj --urls http://127.0.0.1:5000
```

The sample reads:

- `Supabase:Url`
- `Supabase:AnonKey`
- `Supabase:ServiceRoleKey`

`appsettings.Development.json` is already pointed at the local default Supabase stack and anon key, so the sample can run against `supabase start` without extra config.

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

The sample is intentionally stateless and creates a fresh SDK client per request through `ISupabaseStatelessClientFactory`.
It queries the repo-managed `integration_todos` table from the local Supabase stack.

For `/todos/user`, supply a real access token from your auth flow in the standard `Authorization: Bearer ...` header.
