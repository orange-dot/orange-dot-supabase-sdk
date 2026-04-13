#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
SMOKE_ROOT="$REPO_ROOT/unity/SmokeHost"
LOG_FILE="${UNITY_SMOKE_LOG_FILE:-/tmp/unity-smoke-import.log}"

detect_editor() {
  if [[ -n "${UNITY_EDITOR_PATH:-}" && -x "${UNITY_EDITOR_PATH}" ]]; then
    printf '%s\n' "${UNITY_EDITOR_PATH}"
    return 0
  fi

  local latest
  latest="$(find "$HOME/Unity/Hub/Editor" -mindepth 3 -maxdepth 3 -type f -path '*/Editor/Unity' 2>/dev/null | sort -V | tail -n 1)"
  [[ -n "$latest" ]] || return 1
  printf '%s\n' "$latest"
}

bash "$SCRIPT_DIR/prepare-smoke-host.sh"

EDITOR_BIN="$(detect_editor)" || {
  printf 'Could not find a Unity Editor under %s/Unity/Hub/Editor\n' "$HOME" >&2
  exit 1
}

printf 'Using Unity Editor: %s\n' "$EDITOR_BIN"

"$EDITOR_BIN" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$SMOKE_ROOT" \
  -logFile "$LOG_FILE"

printf 'Unity smoke import/compile completed successfully. Log: %s\n' "$LOG_FILE"
