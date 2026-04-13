create or replace function public.research_request_role()
returns text
language sql
stable
as $$
    select coalesce(
        auth.role(),
        nullif(current_setting('request.jwt.claim.role', true), ''),
        case
            when coalesce(current_setting('request.jwt.claims', true), '') = '' then null
            else current_setting('request.jwt.claims', true)::jsonb ->> 'role'
        end
    )
$$;

grant execute on function public.research_request_role() to authenticated, service_role;

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
        and coalesce(public.research_request_role(), '') <> 'service_role' then
        raise exception 'baseline_run_id_must_be_promoted_via_function';
    end if;

    return new;
end;
$$;
