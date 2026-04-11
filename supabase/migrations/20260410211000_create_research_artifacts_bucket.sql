insert into storage.buckets (id, name, public)
values ('research-artifacts', 'research-artifacts', false)
on conflict (id) do update
set
    name = excluded.name,
    public = excluded.public;

create policy research_artifacts_object_read
    on storage.objects
    for select
    to authenticated
    using (public.research_can_access_storage_object(bucket_id, name, 'viewer'));

create policy research_artifacts_object_insert
    on storage.objects
    for insert
    to authenticated
    with check (public.research_can_access_storage_object(bucket_id, name, 'editor'));

create policy research_artifacts_object_update
    on storage.objects
    for update
    to authenticated
    using (public.research_can_access_storage_object(bucket_id, name, 'editor'))
    with check (public.research_can_access_storage_object(bucket_id, name, 'editor'));

create policy research_artifacts_object_delete
    on storage.objects
    for delete
    to authenticated
    using (public.research_can_access_storage_object(bucket_id, name, 'owner'));
