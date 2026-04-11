# ResearchWorkspaceApi

Research workspace sample for the server-side `AddSupabaseServer(...)` flow.

This sample shows one coherent Supabase-backed workflow:

- delegated bearer-token access
- embedded browser cockpit
- Swagger + OpenAPI docs
- multi-tenant RLS
- PostgREST CRUD
- Storage artifact upload
- Edge Function baseline promotion
- Realtime run-status watching

## Layering

The sample is intentionally split into an SDK-backed backend and a thin browser transport layer.

- SDK-backed backend
  The ASP.NET app is the real Orange Dot sample surface. It uses `AddSupabaseServer(...)`, `ISupabaseStatelessClientFactory`, and the Orange Dot orchestration layer for delegated PostgREST, Storage, Functions, and Realtime operations.
- Browser transport
  The embedded cockpit in `wwwroot/` is static HTML, CSS, and JavaScript. It calls the sample API with `fetch` and stores the returned access token in browser storage.
- Direct Auth helper
  The sample-scoped `/ui/auth/signup` and `/ui/auth/login` endpoints use the local Supabase Auth HTTP API with the publishable key to create or exchange an end-user session for the browser. They do not add a separate browser SDK or a server-side cookie session.

In short: the research workflow itself is SDK-backed on the server, while the browser shell is just a lightweight client for exercising that server path.

## Run

```bash
supabase start
dotnet run --project samples/ResearchWorkspaceApi/ResearchWorkspaceApi.csproj --urls http://127.0.0.1:5050
```

After startup, you can use:

- cockpit UI: `http://127.0.0.1:5050/`
- Swagger UI: `http://127.0.0.1:5050/swagger`
- OpenAPI JSON: `http://127.0.0.1:5050/openapi/v1.json`

The sample reads:

- `Supabase:Url`
- `Supabase:PublishableKey`
- `Supabase:SecretKey`

`appsettings.Development.json` is already pointed at the local Supabase stack and local service-role key, so the sample can run against `supabase start` without extra config.

Protected endpoints now return JSON error payloads in this shape:

```json
{
  "status": 401,
  "error": "auth_required",
  "detail": "A bearer token is required.",
  "traceId": "..."
}
```

The embedded cockpit uses sample-scoped `/ui/auth/*` helpers to create or exchange a local Supabase Auth session, then stores the returned access token in browser storage and calls the rest of the API with `Authorization: Bearer ...`.
Everything after that token exchange goes through the sample API, whose research workflow is backed by the Orange Dot SDK on the server.

## Browser workflow

1. Open `http://127.0.0.1:5050/`
2. Sign up or sign in with any local Supabase email/password user
3. Create an organization
4. Add memberships to test `owner`, `editor`, and `viewer` paths
5. Create a project, experiment, and run
6. Append metrics, upload a text artifact, and promote a baseline
7. Start a watcher and then update the run status to observe realtime delivery

The cockpit keeps the current access token only in the browser and does not add any server-side cookie session.
That browser token is then delegated into the Orange Dot server-side client factory for the protected workflow calls.

## Sign up two users

Owner:

```bash
curl -sS \
  -H "apikey: $(jq -r '.Supabase.PublishableKey' samples/ResearchWorkspaceApi/appsettings.Development.json)" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:54321/auth/v1/signup \
  -d '{"email":"owner@example.com","password":"password123"}'
```

Editor:

```bash
curl -sS \
  -H "apikey: $(jq -r '.Supabase.PublishableKey' samples/ResearchWorkspaceApi/appsettings.Development.json)" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:54321/auth/v1/signup \
  -d '{"email":"editor@example.com","password":"password123"}'
```

Exchange email/password for access tokens:

```bash
curl -sS \
  -H "apikey: $(jq -r '.Supabase.PublishableKey' samples/ResearchWorkspaceApi/appsettings.Development.json)" \
  -H "Content-Type: application/json" \
  "http://127.0.0.1:54321/auth/v1/token?grant_type=password" \
  -d '{"email":"owner@example.com","password":"password123"}'
```

Keep the returned `access_token` from each response.

## Happy path

Health:

```bash
curl http://127.0.0.1:5050/health
```

Inspect caller identity:

```bash
curl \
  -H "Authorization: Bearer <owner-access-token>" \
  http://127.0.0.1:5050/me
```

Create an organization:

```bash
curl -sS \
  -H "Authorization: Bearer <owner-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/organizations \
  -d '{"name":"Audio Research Guild"}'
```

Add the editor to the organization:

```bash
curl -sS \
  -H "Authorization: Bearer <owner-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/organizations/<org-id>/memberships \
  -d '{"userId":"<editor-user-id>","role":"editor"}'
```

Create a project and experiment:

```bash
curl -sS \
  -H "Authorization: Bearer <owner-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/organizations/<org-id>/projects \
  -d '{"name":"Cocek Tuning Lab"}'
```

```bash
curl -sS \
  -H "Authorization: Bearer <owner-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/projects/<project-id>/experiments \
  -d '{"name":"Spring Session","summary":"Baseline-friendly run flow","status":"active"}'
```

Create a run and append a metric:

```bash
curl -sS \
  -H "Authorization: Bearer <editor-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/experiments/<experiment-id>/runs \
  -d '{"displayName":"Run 01","notes":"first pass","status":"running"}'
```

```bash
curl -sS \
  -H "Authorization: Bearer <editor-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/runs/<run-id>/metrics \
  -d '{"metricName":"tempoDrift","metricValue":0.17,"metricUnit":"percent"}'
```

Upload a text artifact:

```bash
curl -sS \
  -H "Authorization: Bearer <editor-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/runs/<run-id>/artifacts/text \
  -d '{"kind":"log","fileName":"run.log","content":"render complete","contentType":"text/plain"}'
```

Promote the run to baseline:

```bash
curl -sS \
  -H "Authorization: Bearer <editor-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/experiments/<experiment-id>/baseline \
  -d '{"runId":"<run-id>"}'
```

## Realtime watch

Start a watch:

```bash
curl -sS \
  -H "Authorization: Bearer <editor-access-token>" \
  -X POST \
  http://127.0.0.1:5050/experiments/<experiment-id>/watchers
```

The watcher endpoint intentionally waits a few seconds before returning so the local Supabase Realtime channel is fully armed before you trigger the first status update.

Update the run status:

```bash
curl -sS \
  -H "Authorization: Bearer <editor-access-token>" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:5050/runs/<run-id>/status \
  -d '{"status":"succeeded","notes":"watch should observe this"}'
```

Fetch the watch snapshot:

```bash
curl -sS \
  -H "Authorization: Bearer <editor-access-token>" \
  http://127.0.0.1:5050/watchers/<watch-id>
```

Watch snapshots are scoped to the user who created the watch. Fetching another user's `<watch-id>` returns `403`.

## Live verification

```bash
supabase start
ORANGEDOT_SUPABASE_RUN_INTEGRATION=1 dotnet test tests/OrangeDot.Supabase.IntegrationTests/OrangeDot.Supabase.IntegrationTests.csproj --configuration Release -f net10.0 --filter ResearchWorkspace
```

That slice covers:

- protected sample endpoint auth behavior
- authenticated sample HTTP flow end to end
- tenant isolation
- viewer/editor/owner role enforcement
- artifact storage protection
- baseline promotion function behavior
- realtime watcher delivery

## Expected RLS behavior

- a user with no organization membership gets `401` on protected endpoints or `403`/empty result sets from delegated Supabase operations
- `viewer` can list organizations, projects, experiments, runs, artifacts, and decisions
- `viewer` cannot create runs, metrics, artifacts, or baseline promotions
- `editor` can create runs, metrics, artifacts, decisions, and baseline promotions
- `owner` can additionally manage memberships and delete workspace resources
