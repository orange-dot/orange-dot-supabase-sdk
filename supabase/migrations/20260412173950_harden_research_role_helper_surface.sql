create schema if not exists private;

revoke all on schema private from public;
grant usage on schema private to authenticated, service_role;

create or replace function private.research_has_role(org_id uuid, minimum_role text)
returns boolean
language sql
stable
security definer
set search_path = public, private
as $$
    select exists (
        select 1
        from public.research_memberships memberships
        where memberships.organization_id = org_id
          and memberships.user_id = public.research_current_user_id()
          and public.research_role_rank(memberships.role) >= public.research_role_rank(minimum_role)
    )
$$;

grant execute on function private.research_has_role(uuid, text) to authenticated, service_role;

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

    if not private.research_has_role(requested_org_id, minimum_role) then
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

drop policy if exists research_organizations_select on public.research_organizations;
create policy research_organizations_select
    on public.research_organizations
    for select
    to authenticated
    using (private.research_has_role(id, 'viewer'));

drop policy if exists research_organizations_update on public.research_organizations;
create policy research_organizations_update
    on public.research_organizations
    for update
    to authenticated
    using (private.research_has_role(id, 'owner'))
    with check (private.research_has_role(id, 'owner'));

drop policy if exists research_organizations_delete on public.research_organizations;
create policy research_organizations_delete
    on public.research_organizations
    for delete
    to authenticated
    using (private.research_has_role(id, 'owner'));

drop policy if exists research_memberships_select on public.research_memberships;
create policy research_memberships_select
    on public.research_memberships
    for select
    to authenticated
    using (private.research_has_role(organization_id, 'viewer'));

drop policy if exists research_memberships_insert on public.research_memberships;
create policy research_memberships_insert
    on public.research_memberships
    for insert
    to authenticated
    with check (private.research_has_role(organization_id, 'owner'));

drop policy if exists research_memberships_update on public.research_memberships;
create policy research_memberships_update
    on public.research_memberships
    for update
    to authenticated
    using (private.research_has_role(organization_id, 'owner'))
    with check (private.research_has_role(organization_id, 'owner'));

drop policy if exists research_memberships_delete on public.research_memberships;
create policy research_memberships_delete
    on public.research_memberships
    for delete
    to authenticated
    using (private.research_has_role(organization_id, 'owner'));

drop policy if exists research_projects_select on public.research_projects;
create policy research_projects_select
    on public.research_projects
    for select
    to authenticated
    using (private.research_has_role(organization_id, 'viewer'));

drop policy if exists research_projects_insert on public.research_projects;
create policy research_projects_insert
    on public.research_projects
    for insert
    to authenticated
    with check (private.research_has_role(organization_id, 'editor'));

drop policy if exists research_projects_update on public.research_projects;
create policy research_projects_update
    on public.research_projects
    for update
    to authenticated
    using (private.research_has_role(organization_id, 'editor'))
    with check (private.research_has_role(organization_id, 'editor'));

drop policy if exists research_projects_delete on public.research_projects;
create policy research_projects_delete
    on public.research_projects
    for delete
    to authenticated
    using (private.research_has_role(organization_id, 'owner'));

drop policy if exists research_experiments_select on public.research_experiments;
create policy research_experiments_select
    on public.research_experiments
    for select
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'viewer')
        )
    );

drop policy if exists research_experiments_insert on public.research_experiments;
create policy research_experiments_insert
    on public.research_experiments
    for insert
    to authenticated
    with check (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_experiments_update on public.research_experiments;
create policy research_experiments_update
    on public.research_experiments
    for update
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_experiments_delete on public.research_experiments;
create policy research_experiments_delete
    on public.research_experiments
    for delete
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'owner')
        )
    );

drop policy if exists research_runs_select on public.research_runs;
create policy research_runs_select
    on public.research_runs
    for select
    to authenticated
    using (
        exists (
            select 1
            from public.research_experiments experiments
            join public.research_projects projects on projects.id = experiments.project_id
            where experiments.id = experiment_id
              and private.research_has_role(projects.organization_id, 'viewer')
        )
    );

drop policy if exists research_runs_insert on public.research_runs;
create policy research_runs_insert
    on public.research_runs
    for insert
    to authenticated
    with check (
        public.research_current_user_id() is not null
        and exists (
            select 1
            from public.research_experiments experiments
            join public.research_projects projects on projects.id = experiments.project_id
            where experiments.id = experiment_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_runs_update on public.research_runs;
create policy research_runs_update
    on public.research_runs
    for update
    to authenticated
    using (
        exists (
            select 1
            from public.research_experiments experiments
            join public.research_projects projects on projects.id = experiments.project_id
            where experiments.id = experiment_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_experiments experiments
            join public.research_projects projects on projects.id = experiments.project_id
            where experiments.id = experiment_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_runs_delete on public.research_runs;
create policy research_runs_delete
    on public.research_runs
    for delete
    to authenticated
    using (
        exists (
            select 1
            from public.research_experiments experiments
            join public.research_projects projects on projects.id = experiments.project_id
            where experiments.id = experiment_id
              and private.research_has_role(projects.organization_id, 'owner')
        )
    );

drop policy if exists research_run_metrics_select on public.research_run_metrics;
create policy research_run_metrics_select
    on public.research_run_metrics
    for select
    to authenticated
    using (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'viewer')
        )
    );

drop policy if exists research_run_metrics_insert on public.research_run_metrics;
create policy research_run_metrics_insert
    on public.research_run_metrics
    for insert
    to authenticated
    with check (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_run_metrics_update on public.research_run_metrics;
create policy research_run_metrics_update
    on public.research_run_metrics
    for update
    to authenticated
    using (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_run_metrics_delete on public.research_run_metrics;
create policy research_run_metrics_delete
    on public.research_run_metrics
    for delete
    to authenticated
    using (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'owner')
        )
    );

drop policy if exists research_run_artifacts_select on public.research_run_artifacts;
create policy research_run_artifacts_select
    on public.research_run_artifacts
    for select
    to authenticated
    using (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'viewer')
        )
    );

drop policy if exists research_run_artifacts_insert on public.research_run_artifacts;
create policy research_run_artifacts_insert
    on public.research_run_artifacts
    for insert
    to authenticated
    with check (
        public.research_current_user_id() is not null
        and storage_bucket = 'research-artifacts'
        and exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_run_artifacts_update on public.research_run_artifacts;
create policy research_run_artifacts_update
    on public.research_run_artifacts
    for update
    to authenticated
    using (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_run_artifacts_delete on public.research_run_artifacts;
create policy research_run_artifacts_delete
    on public.research_run_artifacts
    for delete
    to authenticated
    using (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and private.research_has_role(projects.organization_id, 'owner')
        )
    );

drop policy if exists research_decisions_select on public.research_decisions;
create policy research_decisions_select
    on public.research_decisions
    for select
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'viewer')
        )
    );

drop policy if exists research_decisions_insert on public.research_decisions;
create policy research_decisions_insert
    on public.research_decisions
    for insert
    to authenticated
    with check (
        public.research_current_user_id() is not null
        and exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_decisions_update on public.research_decisions;
create policy research_decisions_update
    on public.research_decisions
    for update
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'editor')
        )
    );

drop policy if exists research_decisions_delete on public.research_decisions;
create policy research_decisions_delete
    on public.research_decisions
    for delete
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and private.research_has_role(projects.organization_id, 'owner')
        )
    );

revoke execute on function public.research_has_role(uuid, text) from authenticated, service_role;
drop function public.research_has_role(uuid, text);
