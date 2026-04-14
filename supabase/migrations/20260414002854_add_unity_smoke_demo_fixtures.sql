create table if not exists public.unity_todos (
  id bigint generated always as identity primary key,
  owner_id uuid not null references auth.users (id) on delete cascade,
  title text not null,
  created_at timestamptz not null default now()
);

alter table public.unity_todos enable row level security;

drop policy if exists unity_todos_select_own on public.unity_todos;
create policy unity_todos_select_own
on public.unity_todos
for select
to authenticated
using (auth.uid() = owner_id);

drop policy if exists unity_todos_insert_own on public.unity_todos;
create policy unity_todos_insert_own
on public.unity_todos
for insert
to authenticated
with check (auth.uid() = owner_id);

insert into storage.buckets (id, name, public)
values ('unity-sample', 'unity-sample', false)
on conflict (id) do update
set
  name = excluded.name,
  public = excluded.public;

drop policy if exists unity_sample_select_own_objects on storage.objects;
create policy unity_sample_select_own_objects
on storage.objects
for select
to authenticated
using (
  bucket_id = 'unity-sample'
  and (storage.foldername(name))[1] = (select auth.uid()::text)
);

drop policy if exists unity_sample_insert_own_objects on storage.objects;
create policy unity_sample_insert_own_objects
on storage.objects
for insert
to authenticated
with check (
  bucket_id = 'unity-sample'
  and (storage.foldername(name))[1] = (select auth.uid()::text)
);

drop policy if exists unity_sample_update_own_objects on storage.objects;
create policy unity_sample_update_own_objects
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
