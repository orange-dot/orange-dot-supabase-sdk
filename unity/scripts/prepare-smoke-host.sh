#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
SMOKE_ROOT="$REPO_ROOT/unity/SmokeHost"
STAGE_ROOT="$SMOKE_ROOT/LocalPackages"
SAMPLE_ROOT="$SMOKE_ROOT/Assets/Samples/AuthAndData"

sync_package() {
  local package_name="$1"
  local source_dir="$2"
  local target_dir="$STAGE_ROOT/$package_name"

  mkdir -p "$target_dir"

  rsync -a --delete \
    --delete-excluded \
    --exclude '.git/' \
    --exclude '.vs/' \
    --exclude 'bin/' \
    --exclude 'bin.meta' \
    --exclude 'obj/' \
    --exclude 'obj.meta' \
    --exclude 'TestResults/' \
    --exclude 'Tests/' \
    --exclude 'Tests.meta' \
    --exclude '*.csproj' \
    --exclude '*.csproj.meta' \
    --exclude '*.sln' \
    --exclude '*.sln.meta' \
    --exclude '*.user' \
    --exclude '*.DotSettings.user' \
    "$source_dir/" "$target_dir/"
}

mkdir -p "$STAGE_ROOT"

sync_package "com.orange-dot.vendor.birdmessenger" "$REPO_ROOT/unity/Vendor/BirdMessenger"
sync_package "com.orange-dot.vendor.mimemapping" "$REPO_ROOT/unity/Vendor/MimeMapping"
sync_package "com.orange-dot.supabase.core" "$REPO_ROOT/modules/core-csharp/Core"
sync_package "com.orange-dot.supabase.gotrue" "$REPO_ROOT/modules/gotrue-csharp/Gotrue"
sync_package "com.orange-dot.supabase.postgrest" "$REPO_ROOT/modules/postgrest-csharp/Postgrest"
sync_package "com.orange-dot.supabase.functions" "$REPO_ROOT/modules/functions-csharp/Functions"
sync_package "com.orange-dot.supabase.storage" "$REPO_ROOT/modules/storage-csharp/Storage"
sync_package "com.orange-dot.supabase.unity" "$REPO_ROOT/unity/OrangeDot.Supabase.Unity"

mkdir -p "$SAMPLE_ROOT"
rsync -a --delete \
  --exclude '.meta' \
  "$REPO_ROOT/unity/OrangeDot.Supabase.Unity/Samples~/AuthAndData/" "$SAMPLE_ROOT/"

printf 'Prepared staged Unity packages under %s\n' "$STAGE_ROOT"
printf 'Synced package sample into %s\n' "$SAMPLE_ROOT"
