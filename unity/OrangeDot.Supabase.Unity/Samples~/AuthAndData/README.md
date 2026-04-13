# AuthAndData Sample

This sample is the first Unity-facing slice for the repo:

- restore a cached session on startup
- sign in with email and password
- insert a row into a user-owned table
- query that same table through `Postgrest`
- optionally invoke an authenticated Edge Function
- sign out and clear local state

## Demo Table

Create a simple table and policies in Supabase before running the sample:

```sql
create table if not exists public.unity_todos (
  id bigint generated always as identity primary key,
  owner_id uuid not null references auth.users (id) on delete cascade,
  title text not null,
  created_at timestamptz not null default now()
);

alter table public.unity_todos enable row level security;

create policy "unity_todos_select_own"
on public.unity_todos
for select
to authenticated
using (auth.uid() = owner_id);

create policy "unity_todos_insert_own"
on public.unity_todos
for insert
to authenticated
with check (auth.uid() = owner_id);
```

## Scene Setup

1. Import the sample from Package Manager.
2. Create an empty `GameObject`.
3. Add `AuthAndDataSampleController`.
4. Fill `ProjectUrl` and `AnonKey`.
5. Press Play.

The sample uses `UnitySessionPersistence(Application.persistentDataPath)` so the last session is restored across runs.

## Optional Edge Function

If you want to exercise the `Functions` surface from the same scene, deploy a simple function named `unity-hello`:

```ts
import { serve } from "https://deno.land/std@0.224.0/http/server.ts";

serve(async (req) => {
  const body = await req.json().catch(() => ({}));

  return Response.json({
    ok: true,
    message: body.message ?? "Hello from Unity",
    userId: body.userId ?? null,
    email: body.email ?? null,
    receivedAt: new Date().toISOString()
  });
});
```

Deploy it with:

```bash
supabase functions deploy unity-hello
```

After you sign in through the sample scene, click `Invoke Function` to POST the current user context and display the response in the UI.
