create extension if not exists pgcrypto with schema extensions;

grant usage on schema public to authenticated, service_role;

create or replace function public.research_set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at := timezone('utc', now());
    return new;
end;
$$;

create or replace function public.research_try_uuid(value text)
returns uuid
language plpgsql
immutable
as $$
begin
    if value is null or btrim(value) = '' then
        return null;
    end if;

    return value::uuid;
exception
    when others then
        return null;
end;
$$;

create or replace function public.research_role_rank(value text)
returns integer
language sql
immutable
as $$
    select case value
        when 'viewer' then 10
        when 'editor' then 20
        when 'owner' then 30
        else 0
    end
$$;

create table public.research_organizations
(
    id          uuid primary key default extensions.gen_random_uuid(),
    name        text                     not null,
    created_by  uuid                     not null references auth.users (id) on delete restrict,
    inserted_at timestamptz              not null default timezone('utc', now()),
    updated_at  timestamptz              not null default timezone('utc', now())
);

create table public.research_memberships
(
    id               uuid primary key default extensions.gen_random_uuid(),
    organization_id  uuid                     not null references public.research_organizations (id) on delete cascade,
    user_id          uuid                     not null references auth.users (id) on delete cascade,
    role             text                     not null,
    inserted_at      timestamptz              not null default timezone('utc', now()),
    constraint research_memberships_role_check
        check (role in ('owner', 'editor', 'viewer')),
    constraint research_memberships_unique_membership
        unique (organization_id, user_id)
);

create table public.research_projects
(
    id              uuid primary key default extensions.gen_random_uuid(),
    organization_id uuid                     not null references public.research_organizations (id) on delete cascade,
    name            text                     not null,
    visibility      text                     not null default 'private',
    inserted_at     timestamptz              not null default timezone('utc', now()),
    updated_at      timestamptz              not null default timezone('utc', now()),
    constraint research_projects_visibility_check
        check (visibility in ('private'))
);

create table public.research_experiments
(
    id              uuid primary key default extensions.gen_random_uuid(),
    project_id       uuid                     not null references public.research_projects (id) on delete cascade,
    name             text                     not null,
    summary          text,
    status           text                     not null default 'draft',
    baseline_run_id  uuid,
    inserted_at      timestamptz              not null default timezone('utc', now()),
    updated_at       timestamptz              not null default timezone('utc', now()),
    constraint research_experiments_status_check
        check (status in ('draft', 'active', 'archived'))
);

create table public.research_runs
(
    id            uuid primary key default extensions.gen_random_uuid(),
    experiment_id uuid                     not null references public.research_experiments (id) on delete cascade,
    display_name  text                     not null,
    notes         text,
    status        text                     not null default 'queued',
    created_by    uuid                     not null references auth.users (id) on delete restrict,
    inserted_at   timestamptz              not null default timezone('utc', now()),
    updated_at    timestamptz              not null default timezone('utc', now()),
    started_at    timestamptz,
    completed_at  timestamptz,
    constraint research_runs_status_check
        check (status in ('queued', 'running', 'succeeded', 'failed', 'canceled'))
);

alter table public.research_experiments
    add constraint research_experiments_baseline_run_fk
        foreign key (baseline_run_id) references public.research_runs (id) on delete set null;

create table public.research_run_metrics
(
    id            uuid primary key default extensions.gen_random_uuid(),
    run_id         uuid                     not null references public.research_runs (id) on delete cascade,
    metric_name    text                     not null,
    metric_value   double precision         not null,
    metric_unit    text,
    inserted_at    timestamptz              not null default timezone('utc', now())
);

create table public.research_run_artifacts
(
    id             uuid primary key default extensions.gen_random_uuid(),
    run_id          uuid                     not null references public.research_runs (id) on delete cascade,
    storage_bucket  text                     not null default 'research-artifacts',
    object_path     text                     not null,
    file_name       text                     not null,
    kind            text                     not null,
    content_type    text,
    uploaded_by     uuid                     not null references auth.users (id) on delete restrict,
    inserted_at     timestamptz              not null default timezone('utc', now()),
    updated_at      timestamptz              not null default timezone('utc', now()),
    constraint research_run_artifacts_kind_check
        check (kind in ('log', 'report', 'bundle')),
    constraint research_run_artifacts_unique_object_path
        unique (storage_bucket, object_path)
);

create table public.research_decisions
(
    id              uuid primary key default extensions.gen_random_uuid(),
    project_id       uuid                     not null references public.research_projects (id) on delete cascade,
    experiment_id    uuid references public.research_experiments (id) on delete set null,
    baseline_run_id  uuid references public.research_runs (id) on delete set null,
    title            text                     not null,
    summary          text,
    status           text                     not null default 'proposed',
    created_by       uuid                     not null references auth.users (id) on delete restrict,
    inserted_at      timestamptz              not null default timezone('utc', now()),
    updated_at       timestamptz              not null default timezone('utc', now()),
    constraint research_decisions_status_check
        check (status in ('proposed', 'accepted', 'rejected'))
);

create index research_memberships_user_idx on public.research_memberships (user_id, organization_id);
create index research_projects_org_idx on public.research_projects (organization_id);
create index research_experiments_project_idx on public.research_experiments (project_id);
create index research_runs_experiment_idx on public.research_runs (experiment_id, inserted_at desc);
create index research_run_metrics_run_idx on public.research_run_metrics (run_id, inserted_at desc);
create index research_run_artifacts_run_idx on public.research_run_artifacts (run_id, inserted_at desc);
create index research_decisions_project_idx on public.research_decisions (project_id, inserted_at desc);

create or replace function public.research_has_role(org_id uuid, minimum_role text)
returns boolean
language sql
stable
as $$
    select exists (
        select 1
        from public.research_memberships memberships
        where memberships.organization_id = org_id
          and memberships.user_id = auth.uid()
          and public.research_role_rank(memberships.role) >= public.research_role_rank(minimum_role)
    )
$$;

create or replace function public.research_can_access_storage_object(bucket text, object_name text, minimum_role text)
returns boolean
language plpgsql
stable
as $$
declare
    org_id uuid := public.research_try_uuid(split_part(object_name, '/', 2));
    project_id uuid := public.research_try_uuid(split_part(object_name, '/', 4));
    run_id uuid := public.research_try_uuid(split_part(object_name, '/', 6));
begin
    if bucket <> 'research-artifacts' then
        return false;
    end if;

    if split_part(object_name, '/', 1) <> 'org'
        or split_part(object_name, '/', 3) <> 'project'
        or split_part(object_name, '/', 5) <> 'run'
        or org_id is null
        or project_id is null
        or run_id is null then
        return false;
    end if;

    if not public.research_has_role(org_id, minimum_role) then
        return false;
    end if;

    return exists (
        select 1
        from public.research_runs runs
        join public.research_experiments experiments on experiments.id = runs.experiment_id
        join public.research_projects projects on projects.id = experiments.project_id
        where runs.id = run_id
          and projects.id = project_id
          and projects.organization_id = org_id
    );
end;
$$;

create or replace function public.research_seed_owner_membership()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    insert into public.research_memberships (organization_id, user_id, role)
    values (new.id, new.created_by, 'owner')
    on conflict (organization_id, user_id) do nothing;

    return new;
end;
$$;

create or replace function public.research_enforce_baseline_run()
returns trigger
language plpgsql
as $$
begin
    if new.baseline_run_id is null then
        return new;
    end if;

    if not exists (
        select 1
        from public.research_runs runs
        where runs.id = new.baseline_run_id
          and runs.experiment_id = new.id
    ) then
        raise exception 'baseline_run_id_must_belong_to_experiment';
    end if;

    if tg_op = 'UPDATE'
        and new.baseline_run_id is distinct from old.baseline_run_id
        and coalesce(current_setting('request.jwt.claim.role', true), '') <> 'service_role' then
        raise exception 'baseline_run_id_must_be_promoted_via_function';
    end if;

    return new;
end;
$$;

create trigger research_organizations_seed_owner_membership
    after insert on public.research_organizations
    for each row
execute function public.research_seed_owner_membership();

create trigger research_organizations_set_updated_at
    before update on public.research_organizations
    for each row
execute function public.research_set_updated_at();

create trigger research_projects_set_updated_at
    before update on public.research_projects
    for each row
execute function public.research_set_updated_at();

create trigger research_experiments_set_updated_at
    before update on public.research_experiments
    for each row
execute function public.research_set_updated_at();

create trigger research_runs_set_updated_at
    before update on public.research_runs
    for each row
execute function public.research_set_updated_at();

create trigger research_run_artifacts_set_updated_at
    before update on public.research_run_artifacts
    for each row
execute function public.research_set_updated_at();

create trigger research_decisions_set_updated_at
    before update on public.research_decisions
    for each row
execute function public.research_set_updated_at();

create trigger research_experiments_enforce_baseline_run
    before insert or update on public.research_experiments
    for each row
execute function public.research_enforce_baseline_run();

alter table public.research_runs replica identity full;
alter table public.research_decisions replica identity full;

do $$
begin
    if not exists (
        select 1
        from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'research_runs'
    ) then
        alter publication supabase_realtime add table public.research_runs;
    end if;

    if not exists (
        select 1
        from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'research_decisions'
    ) then
        alter publication supabase_realtime add table public.research_decisions;
    end if;
end
$$;

grant select, insert, update, delete on table public.research_organizations to authenticated, service_role;
grant select, insert, update, delete on table public.research_memberships to authenticated, service_role;
grant select, insert, update, delete on table public.research_projects to authenticated, service_role;
grant select, insert, update, delete on table public.research_experiments to authenticated, service_role;
grant select, insert, update, delete on table public.research_runs to authenticated, service_role;
grant select, insert, update, delete on table public.research_run_metrics to authenticated, service_role;
grant select, insert, update, delete on table public.research_run_artifacts to authenticated, service_role;
grant select, insert, update, delete on table public.research_decisions to authenticated, service_role;

grant execute on function public.research_role_rank(text) to authenticated, service_role;
grant execute on function public.research_has_role(uuid, text) to authenticated, service_role;
grant execute on function public.research_can_access_storage_object(text, text, text) to authenticated, service_role;

alter table public.research_organizations enable row level security;
alter table public.research_memberships enable row level security;
alter table public.research_projects enable row level security;
alter table public.research_experiments enable row level security;
alter table public.research_runs enable row level security;
alter table public.research_run_metrics enable row level security;
alter table public.research_run_artifacts enable row level security;
alter table public.research_decisions enable row level security;

create policy research_organizations_select
    on public.research_organizations
    for select
    to authenticated
    using (public.research_has_role(id, 'viewer'));

create policy research_organizations_insert
    on public.research_organizations
    for insert
    to authenticated
    with check (created_by = auth.uid());

create policy research_organizations_update
    on public.research_organizations
    for update
    to authenticated
    using (public.research_has_role(id, 'owner'))
    with check (public.research_has_role(id, 'owner'));

create policy research_organizations_delete
    on public.research_organizations
    for delete
    to authenticated
    using (public.research_has_role(id, 'owner'));

create policy research_memberships_select
    on public.research_memberships
    for select
    to authenticated
    using (public.research_has_role(organization_id, 'viewer'));

create policy research_memberships_insert
    on public.research_memberships
    for insert
    to authenticated
    with check (public.research_has_role(organization_id, 'owner'));

create policy research_memberships_update
    on public.research_memberships
    for update
    to authenticated
    using (public.research_has_role(organization_id, 'owner'))
    with check (public.research_has_role(organization_id, 'owner'));

create policy research_memberships_delete
    on public.research_memberships
    for delete
    to authenticated
    using (public.research_has_role(organization_id, 'owner'));

create policy research_projects_select
    on public.research_projects
    for select
    to authenticated
    using (public.research_has_role(organization_id, 'viewer'));

create policy research_projects_insert
    on public.research_projects
    for insert
    to authenticated
    with check (public.research_has_role(organization_id, 'editor'));

create policy research_projects_update
    on public.research_projects
    for update
    to authenticated
    using (public.research_has_role(organization_id, 'editor'))
    with check (public.research_has_role(organization_id, 'editor'));

create policy research_projects_delete
    on public.research_projects
    for delete
    to authenticated
    using (public.research_has_role(organization_id, 'owner'));

create policy research_experiments_select
    on public.research_experiments
    for select
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'viewer')
        )
    );

create policy research_experiments_insert
    on public.research_experiments
    for insert
    to authenticated
    with check (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

create policy research_experiments_update
    on public.research_experiments
    for update
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

create policy research_experiments_delete
    on public.research_experiments
    for delete
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'owner')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'viewer')
        )
    );

create policy research_runs_insert
    on public.research_runs
    for insert
    to authenticated
    with check (
        created_by = auth.uid()
        and exists (
            select 1
            from public.research_experiments experiments
            join public.research_projects projects on projects.id = experiments.project_id
            where experiments.id = experiment_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_experiments experiments
            join public.research_projects projects on projects.id = experiments.project_id
            where experiments.id = experiment_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'owner')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'viewer')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'owner')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'viewer')
        )
    );

create policy research_run_artifacts_insert
    on public.research_run_artifacts
    for insert
    to authenticated
    with check (
        uploaded_by = auth.uid()
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
              and public.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_runs runs
            join public.research_experiments experiments on experiments.id = runs.experiment_id
            join public.research_projects projects on projects.id = experiments.project_id
            where runs.id = run_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

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
              and public.research_has_role(projects.organization_id, 'owner')
        )
    );

create policy research_decisions_select
    on public.research_decisions
    for select
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'viewer')
        )
    );

create policy research_decisions_insert
    on public.research_decisions
    for insert
    to authenticated
    with check (
        created_by = auth.uid()
        and exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

create policy research_decisions_update
    on public.research_decisions
    for update
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    )
    with check (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'editor')
        )
    );

create policy research_decisions_delete
    on public.research_decisions
    for delete
    to authenticated
    using (
        exists (
            select 1
            from public.research_projects projects
            where projects.id = project_id
              and public.research_has_role(projects.organization_id, 'owner')
        )
    );
