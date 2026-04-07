create extension if not exists pgcrypto with schema extensions;

grant usage on schema public to anon, authenticated, service_role;

create table public.integration_todos
(
    id          uuid primary key default extensions.gen_random_uuid(),
    details     text                     not null,
    owner_tag   text                     not null,
    inserted_at timestamptz              not null default timezone('utc', now())
);

alter table public.integration_todos replica identity full;
alter publication supabase_realtime add table public.integration_todos;

grant select, insert, update, delete on table public.integration_todos to anon, authenticated, service_role;
