create or replace function public.research_current_user_id()
returns uuid
language sql
stable
as $$
    select coalesce(
        auth.uid(),
        public.research_try_uuid(current_setting('request.jwt.claim.sub', true)),
        public.research_try_uuid(
            case
                when coalesce(current_setting('request.jwt.claims', true), '') = '' then null
                else (current_setting('request.jwt.claims', true)::jsonb ->> 'sub')
            end
        )
    )
$$;

create or replace function public.research_has_role(org_id uuid, minimum_role text)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
    select exists (
        select 1
        from public.research_memberships memberships
        where memberships.organization_id = org_id
          and memberships.user_id = public.research_current_user_id()
          and public.research_role_rank(memberships.role) >= public.research_role_rank(minimum_role)
    )
$$;

create or replace function public.research_assign_request_user()
returns trigger
language plpgsql
as $$
declare
    request_user uuid := public.research_current_user_id();
begin
    if request_user is null then
        raise exception 'authenticated_user_required';
    end if;

    case tg_table_name
        when 'research_organizations' then
            new.created_by := request_user;
        when 'research_runs' then
            new.created_by := request_user;
        when 'research_run_artifacts' then
            new.uploaded_by := request_user;
        when 'research_decisions' then
            new.created_by := request_user;
        else
            raise exception 'unsupported_research_assign_request_user_table:%', tg_table_name;
    end case;

    return new;
end;
$$;

grant execute on function public.research_current_user_id() to authenticated, service_role;
grant execute on function public.research_has_role(uuid, text) to authenticated, service_role;

alter table public.research_organizations
    alter column created_by set default public.research_current_user_id();

alter table public.research_runs
    alter column created_by set default public.research_current_user_id();

alter table public.research_run_artifacts
    alter column uploaded_by set default public.research_current_user_id();

alter table public.research_decisions
    alter column created_by set default public.research_current_user_id();

drop trigger if exists research_organizations_assign_request_user on public.research_organizations;
create trigger research_organizations_assign_request_user
    before insert on public.research_organizations
    for each row
execute function public.research_assign_request_user();

drop trigger if exists research_runs_assign_request_user on public.research_runs;
create trigger research_runs_assign_request_user
    before insert on public.research_runs
    for each row
execute function public.research_assign_request_user();

drop trigger if exists research_run_artifacts_assign_request_user on public.research_run_artifacts;
create trigger research_run_artifacts_assign_request_user
    before insert on public.research_run_artifacts
    for each row
execute function public.research_assign_request_user();

drop trigger if exists research_decisions_assign_request_user on public.research_decisions;
create trigger research_decisions_assign_request_user
    before insert on public.research_decisions
    for each row
execute function public.research_assign_request_user();

drop policy if exists research_organizations_insert on public.research_organizations;
create policy research_organizations_insert
    on public.research_organizations
    for insert
    to authenticated
    with check (public.research_current_user_id() is not null);

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
              and public.research_has_role(projects.organization_id, 'editor')
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
              and public.research_has_role(projects.organization_id, 'editor')
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
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );
