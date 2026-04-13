create or replace function public.research_can_access_storage_object(bucket text, object_name text, minimum_role text)
returns boolean
language plpgsql
stable
as $$
declare
    requested_org_id uuid := public.research_try_uuid(split_part(object_name, '/', 2));
    requested_project_id uuid := public.research_try_uuid(split_part(object_name, '/', 4));
    requested_run_id uuid := public.research_try_uuid(split_part(object_name, '/', 6));
begin
    if bucket <> 'research-artifacts' then
        return false;
    end if;

    if split_part(object_name, '/', 1) <> 'org'
        or split_part(object_name, '/', 3) <> 'project'
        or split_part(object_name, '/', 5) <> 'run'
        or requested_org_id is null
        or requested_project_id is null
        or requested_run_id is null then
        return false;
    end if;

    if not public.research_has_role(requested_org_id, minimum_role) then
        return false;
    end if;

    return exists (
        select 1
        from public.research_runs runs
        join public.research_experiments experiments on experiments.id = runs.experiment_id
        join public.research_projects projects on projects.id = experiments.project_id
        where runs.id = requested_run_id
          and projects.id = requested_project_id
          and projects.organization_id = requested_org_id
    );
end;
$$;
