#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
MIGRATION_FILE="$REPO_ROOT/supabase/migrations/20260414002854_add_unity_smoke_demo_fixtures.sql"

DEFAULT_API_URL="http://127.0.0.1:54321"
DEFAULT_DB_URL="postgresql://postgres:postgres@127.0.0.1:54322/postgres"
DEFAULT_SERVICE_ROLE_KEY="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImV4cCI6MTk4MzgxMjk5Nn0.EGIM96RAZx35lJzdJsyH-qQwv8Hdp7fsn3W0YpN81IU"
DEFAULT_DB_CONTAINER="supabase_db_orange-dot-supabase-sdk"

UNITY_SMOKE_EMAIL="${UNITY_SMOKE_EMAIL:-unity@example.com}"
UNITY_SMOKE_PASSWORD="${UNITY_SMOKE_PASSWORD:-password123}"
UNITY_SMOKE_AUTO_START="${UNITY_SMOKE_AUTO_START:-1}"

API_URL="${SUPABASE_URL:-$DEFAULT_API_URL}"
DB_URL="${SUPABASE_DB_URL:-$DEFAULT_DB_URL}"
SERVICE_ROLE_KEY="${SUPABASE_SERVICE_ROLE_KEY:-$DEFAULT_SERVICE_ROLE_KEY}"

find_command() {
  local name="$1"
  shift

  if command -v "$name" >/dev/null 2>&1; then
    command -v "$name"
    return 0
  fi

  while (($# > 0)); do
    if [[ -x "$1" ]]; then
      printf '%s\n' "$1"
      return 0
    fi

    shift
  done

  return 1
}

load_supabase_env() {
  local tmp_file
  tmp_file="$(mktemp)"
  "$SUPABASE_BIN" status -o env | "$RG_BIN" '^[A-Z_]+=' > "$tmp_file"
  # shellcheck disable=SC1090
  source "$tmp_file"
  rm -f "$tmp_file"

  API_URL="${API_URL:-$DEFAULT_API_URL}"
  DB_URL="${DB_URL:-$DEFAULT_DB_URL}"
  SERVICE_ROLE_KEY="${SERVICE_ROLE_KEY:-$DEFAULT_SERVICE_ROLE_KEY}"
}

ensure_local_stack() {
  if "$SUPABASE_BIN" status -o env >/dev/null 2>&1; then
    load_supabase_env
    return 0
  fi

  if [[ "$UNITY_SMOKE_AUTO_START" != "1" ]]; then
    printf 'Local Supabase stack is not running and UNITY_SMOKE_AUTO_START=0.\n' >&2
    exit 1
  fi

  printf '[unity-smoke-bootstrap] Local Supabase stack not detected. Starting it now...\n'
  (
    cd "$REPO_ROOT"
    "$SUPABASE_BIN" start
  )
  load_supabase_env
}

apply_demo_schema() {
  printf '[unity-smoke-bootstrap] Applying Unity smoke fixtures via %s\n' "$MIGRATION_FILE"
  if [[ -n "${PSQL_BIN:-}" ]]; then
    "$PSQL_BIN" "$DB_URL" -v ON_ERROR_STOP=1 -f "$MIGRATION_FILE" >/dev/null
    return 0
  fi

  "$DOCKER_BIN" exec -i "$DB_CONTAINER" psql -U postgres -d postgres -v ON_ERROR_STOP=1 < "$MIGRATION_FILE" >/dev/null
}

ensure_demo_user() {
  local email_sql user_id payload_file http_code auth_url token_code
  auth_url="${API_URL%/}/auth/v1"
  email_sql="${UNITY_SMOKE_EMAIL//\'/\'\'}"

  if [[ -n "${PSQL_BIN:-}" ]]; then
    user_id="$("$PSQL_BIN" "$DB_URL" -Atqc "select id::text from auth.users where email = '$email_sql' limit 1")"
  else
    user_id="$("$DOCKER_BIN" exec -i "$DB_CONTAINER" psql -U postgres -d postgres -Atqc "select id::text from auth.users where email = '$email_sql' limit 1")"
  fi

  payload_file="$(mktemp)"
  cat > "$payload_file" <<JSON
{
  "email": "$UNITY_SMOKE_EMAIL",
  "password": "$UNITY_SMOKE_PASSWORD",
  "email_confirm": true,
  "user_metadata": {
    "display_name": "Unity Smoke User"
  }
}
JSON

  if [[ -z "$user_id" ]]; then
    printf '[unity-smoke-bootstrap] Creating local demo user %s\n' "$UNITY_SMOKE_EMAIL"
    http_code="$(
      "$CURL_BIN" -sS -o /tmp/unity-smoke-user-create.json -w '%{http_code}' \
        -X POST "$auth_url/admin/users" \
        -H "apikey: $SERVICE_ROLE_KEY" \
        -H "Authorization: Bearer $SERVICE_ROLE_KEY" \
        -H 'Content-Type: application/json' \
        --data @"$payload_file"
    )"

    if [[ "$http_code" != "200" && "$http_code" != "201" ]]; then
      printf 'Creating local demo user failed with HTTP %s\n' "$http_code" >&2
      cat /tmp/unity-smoke-user-create.json >&2
      rm -f "$payload_file"
      exit 1
    fi
  else
    printf '[unity-smoke-bootstrap] Updating local demo user %s\n' "$UNITY_SMOKE_EMAIL"
    http_code="$(
      "$CURL_BIN" -sS -o /tmp/unity-smoke-user-update.json -w '%{http_code}' \
        -X PUT "$auth_url/admin/users/$user_id" \
        -H "apikey: $SERVICE_ROLE_KEY" \
        -H "Authorization: Bearer $SERVICE_ROLE_KEY" \
        -H 'Content-Type: application/json' \
        --data @"$payload_file"
    )"

    if [[ "$http_code" != "200" ]]; then
      printf 'Updating local demo user failed with HTTP %s\n' "$http_code" >&2
      cat /tmp/unity-smoke-user-update.json >&2
      rm -f "$payload_file"
      exit 1
    fi
  fi

  rm -f "$payload_file"

  token_code="$(
    "$CURL_BIN" -sS -o /tmp/unity-smoke-password-signin.json -w '%{http_code}' \
      -X POST "$auth_url/token?grant_type=password" \
      -H "apikey: ${ANON_KEY:-}" \
      -H 'Content-Type: application/json' \
      --data "{\"email\":\"$UNITY_SMOKE_EMAIL\",\"password\":\"$UNITY_SMOKE_PASSWORD\"}"
  )"

  if [[ "$token_code" != "200" ]]; then
    printf 'Password sign-in verification failed with HTTP %s\n' "$token_code" >&2
    cat /tmp/unity-smoke-password-signin.json >&2
    exit 1
  fi
}

print_summary() {
  printf '\nUnity SmokeHost backend is ready.\n'
  printf '  API URL: %s\n' "$API_URL"
  printf '  Demo user: %s\n' "$UNITY_SMOKE_EMAIL"
  printf '  Demo password: %s\n' "$UNITY_SMOKE_PASSWORD"
  printf '  Table: public.unity_todos\n'
  printf '  Bucket: unity-sample\n'
  printf '  Function: orangedot-integration-smoke\n'
}

CURL_BIN="$(find_command curl /usr/bin/curl)" || {
  printf 'Missing required command: curl\n' >&2
  exit 1
}

PSQL_BIN="$(find_command psql /usr/bin/psql /usr/local/bin/psql || true)"

RG_BIN="$(find_command rg /usr/bin/rg /usr/local/bin/rg)" || {
  printf 'Missing required command: rg\n' >&2
  exit 1
}

SUPABASE_BIN="$(find_command supabase "$HOME/.local/bin/supabase")" || {
  printf 'Missing required command: supabase\n' >&2
  exit 1
}

DOCKER_BIN="$(find_command docker /usr/bin/docker /usr/local/bin/docker || true)"
DB_CONTAINER="${SUPABASE_DB_CONTAINER:-$DEFAULT_DB_CONTAINER}"

if [[ -z "${PSQL_BIN:-}" && -z "${DOCKER_BIN:-}" ]]; then
  printf 'Missing required command: psql or docker\n' >&2
  exit 1
fi

ensure_local_stack
apply_demo_schema
ensure_demo_user
print_summary
