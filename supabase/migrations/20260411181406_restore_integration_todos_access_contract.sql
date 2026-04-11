-- Restore the integration smoke table to the repo-managed anon-access contract.
-- The integration test table is intentionally public to exercise the default
-- anon-key Postgrest and Realtime flows without requiring an authenticated user.
alter table public.integration_todos disable row level security;

grant select, insert, update, delete on table public.integration_todos to anon, authenticated, service_role;
