# AuthAndData Sample

This sample is the first Unity-facing slice for the repo:

- restore a cached session on startup
- sign in with email and password
- insert a row into a user-owned table
- query that same table through `Postgrest`
- optionally invoke an authenticated Edge Function
- upload text bytes to a user-scoped path in Storage
- list files for the signed-in user prefix
- create a signed URL for the last uploaded object
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

## Storage Bucket

Create a private bucket named `unity-sample` in Supabase Storage before running the storage part of the sample.

Then add authenticated policies scoped to the current user's top-level folder:

```sql
create policy "unity_sample_select_own_objects"
on storage.objects
for select
to authenticated
using (
  bucket_id = 'unity-sample'
  and (storage.foldername(name))[1] = (select auth.uid()::text)
);

create policy "unity_sample_insert_own_objects"
on storage.objects
for insert
to authenticated
with check (
  bucket_id = 'unity-sample'
  and (storage.foldername(name))[1] = (select auth.uid()::text)
);

create policy "unity_sample_update_own_objects"
on storage.objects
for update
to authenticated
using (
  bucket_id = 'unity-sample'
  and (storage.foldername(name))[1] = (select auth.uid()::text)
)
with check (
  bucket_id = 'unity-sample'
  and (storage.foldername(name))[1] = (select auth.uid()::text)
);
```

The sample uploads text to a deterministic object path under `<user-id>/...` inside the `unity-sample` bucket, lists files under that same prefix, and creates a signed URL for the last uploaded object. The upload path uses `upsert`, so the update policy is required if you upload the same sample file name more than once.

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

## Suggested Demo Flow

1. Press `Initialize`.
2. Sign in with a real user account.
3. Insert and load rows from `unity_todos`.
4. Optionally invoke `unity-hello`.
5. Upload sample bytes to `unity-sample`.
6. List bucket files for the current user prefix.
7. Create a signed URL for the uploaded object.
8. Sign out and confirm rows, function output, file list, and signed URL state are cleared.
