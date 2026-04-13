insert into storage.buckets (id, name, public)
values ('integration-public', 'integration-public', true)
on conflict (id) do update
set
    name = excluded.name,
    public = excluded.public;

do $$
begin
    if not exists (
        select 1
        from pg_policies
        where schemaname = 'storage'
          and tablename = 'objects'
          and policyname = 'integration_public_bucket_object_access'
    ) then
        create policy integration_public_bucket_object_access
            on storage.objects
            for all
            to anon, authenticated, service_role
            using (bucket_id = 'integration-public')
            with check (bucket_id = 'integration-public');
    end if;
end
$$;
