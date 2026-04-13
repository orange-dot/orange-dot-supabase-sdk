#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"

UNITY_HUB_REPO_FILE="/etc/yum.repos.d/unityhub.repo"
UNITY_HUB_REPO_URL="https://hub.unity3d.com/linux/repos/rpm/stable"
UNITY_HUB_GPG_KEY_URL="https://hub.unity3d.com/linux/repos/rpm/stable/repodata/repomd.xml.key"

# Verified from official Unity release pages on 2026-04-13.
LATEST_UNITY_VERSION="6000.3.13f1"
LATEST_UNITY_RELEASE_DATE="2026-04-08"
LATEST_2022_LTS_VERSION="2022.3.75f1"
LATEST_2022_LTS_RELEASE_DATE="2026-04-08"

DEFAULT_EDITOR_VERSION="${UNITY_EDITOR_VERSION:-$LATEST_UNITY_VERSION}"
INSTALL_UNITY_EDITOR="${INSTALL_UNITY_EDITOR:-0}"
LAUNCH_UNITY_HUB="${LAUNCH_UNITY_HUB:-1}"

log() {
  printf '[unity-install] %s\n' "$*"
}

fail() {
  printf '[unity-install] ERROR: %s\n' "$*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Missing required command: $1"
}

detect_hub_binary() {
  if command -v unityhub >/dev/null 2>&1; then
    command -v unityhub
    return 0
  fi

  if [[ -x /opt/unityhub/unityhub ]]; then
    printf '%s\n' /opt/unityhub/unityhub
    return 0
  fi

  return 1
}

check_os() {
  [[ -f /etc/os-release ]] || fail "Cannot read /etc/os-release"

  if ! grep -Eqi '(^ID=fedora$|^ID="?rhel"?$|^ID="?centos"?$|^ID="?rocky"?$|^ID="?almalinux"?$|^ID_LIKE=.*(rhel|fedora).*)' /etc/os-release; then
    fail "This script is for Fedora/RHEL-like systems"
  fi
}

install_unity_hub_repo() {
  log "Writing Unity Hub RPM repository to $UNITY_HUB_REPO_FILE"
  sudo tee "$UNITY_HUB_REPO_FILE" >/dev/null <<EOF
[unityhub]
name=Unity Hub
baseurl=$UNITY_HUB_REPO_URL
enabled=1
gpgcheck=1
gpgkey=$UNITY_HUB_GPG_KEY_URL
repo_gpgcheck=1
EOF
}

install_unity_hub() {
  log "Installing Unity Hub from the official Unity RPM repository"
  sudo dnf check-update || true
  sudo dnf install -y unityhub
}

maybe_install_editor() {
  local hub_bin="$1"
  local help_output
  local install_output

  [[ "$INSTALL_UNITY_EDITOR" == "1" ]] || return 0

  log "Attempting optional headless editor install for $DEFAULT_EDITOR_VERSION"
  log "This can still require Unity Hub login/licensing state on your machine."

  help_output="$("$hub_bin" --help 2>&1 || true)"

  if grep -q -- '--headless' <<<"$help_output"; then
    set +e
    install_output="$("$hub_bin" -- --headless install --version "$DEFAULT_EDITOR_VERSION" 2>&1)"
    local exit_code=$?
    set -e

    if [[ $exit_code -eq 0 ]]; then
      log "Unity Editor $DEFAULT_EDITOR_VERSION installed successfully via Hub CLI."
      return 0
    fi

    printf '%s\n' "$install_output"
    log "Headless editor install did not complete. Most often this means Hub login or license activation is still needed."
    return 0
  fi

  log "This Unity Hub build does not expose a detectable '--headless' CLI in --help."
  log "Install the editor through the Hub GUI after sign-in."
}

maybe_launch_hub() {
  local hub_bin="$1"

  [[ "$LAUNCH_UNITY_HUB" == "1" ]] || return 0

  if [[ -z "${DISPLAY:-}" && -z "${WAYLAND_DISPLAY:-}" ]]; then
    log "No graphical session detected, so I am not auto-launching Unity Hub."
    return 0
  fi

  log "Launching Unity Hub"
  nohup "$hub_bin" >/tmp/unityhub.log 2>&1 &
}

print_next_steps() {
  local hub_bin="$1"

  cat <<EOF

Unity Hub install is done.

Latest overall Unity (official release page checked 2026-04-13):
  $LATEST_UNITY_VERSION  released $LATEST_UNITY_RELEASE_DATE

Latest 2022.3 LTS (useful baseline for this repo's current package manifest):
  $LATEST_2022_LTS_VERSION  released $LATEST_2022_LTS_RELEASE_DATE

Detected Hub binary:
  $hub_bin

Recommended next steps:
  1. Sign in to Unity Hub with your Unity account.
  2. Activate a Personal or Pro license if Hub asks.
  3. Install one editor version:
     - latest overall: $LATEST_UNITY_VERSION
     - repo-baseline LTS: $LATEST_2022_LTS_VERSION
  4. Create or open a local Unity project for package smoke testing.
  5. Add these local packages from disk in this order:
     - $REPO_ROOT/unity/Vendor/BirdMessenger/package.json
     - $REPO_ROOT/unity/Vendor/MimeMapping/package.json
     - $REPO_ROOT/modules/core-csharp/Core/package.json
     - $REPO_ROOT/modules/gotrue-csharp/Gotrue/package.json
     - $REPO_ROOT/modules/postgrest-csharp/Postgrest/package.json
     - $REPO_ROOT/modules/functions-csharp/Functions/package.json
     - $REPO_ROOT/modules/storage-csharp/Storage/package.json
     - $REPO_ROOT/unity/OrangeDot.Supabase.Unity/package.json

Useful flags:
  UNITY_EDITOR_VERSION=<version>   choose a different editor version
  INSTALL_UNITY_EDITOR=1           try optional Hub CLI editor install
  LAUNCH_UNITY_HUB=0               skip auto-launching the Hub

Example:
  INSTALL_UNITY_EDITOR=1 UNITY_EDITOR_VERSION=$LATEST_UNITY_VERSION bash $REPO_ROOT/unity/scripts/install-unity-fedora.sh

EOF
}

main() {
  require_command sudo
  require_command tee
  require_command dnf

  check_os
  install_unity_hub_repo
  install_unity_hub

  local hub_bin
  hub_bin="$(detect_hub_binary)" || fail "Unity Hub installed, but no unityhub binary was found on PATH"

  maybe_install_editor "$hub_bin"
  maybe_launch_hub "$hub_bin"
  print_next_steps "$hub_bin"
}

main "$@"
