#!/usr/bin/env bash
set -euo pipefail

BREW_INSTALL_URL="https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh"
LINUXBREW_BIN="/home/linuxbrew/.linuxbrew/bin/brew"
MACOS_ARM_BIN="/opt/homebrew/bin/brew"
MACOS_INTEL_BIN="/usr/local/bin/brew"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

detect_brew_bin() {
  if command -v brew >/dev/null 2>&1; then
    command -v brew
    return 0
  fi

  for candidate in "$LINUXBREW_BIN" "$MACOS_ARM_BIN" "$MACOS_INTEL_BIN"; do
    if [[ -x "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

append_shellenv_if_missing() {
  local brew_bin="$1"
  local shellenv_line

  shellenv_line="eval \"\$(${brew_bin} shellenv)\""

  if [[ ! -f "${HOME}/.bashrc" ]]; then
    touch "${HOME}/.bashrc"
  fi

  if ! grep -Fq "$shellenv_line" "${HOME}/.bashrc"; then
    printf '\n%s\n' "$shellenv_line" >> "${HOME}/.bashrc"
    echo "Added Homebrew shellenv to ${HOME}/.bashrc"
  else
    echo "Homebrew shellenv already present in ${HOME}/.bashrc"
  fi

  eval "$("${brew_bin}" shellenv)"
}

install_homebrew_if_needed() {
  local brew_bin

  if brew_bin="$(detect_brew_bin)"; then
    echo "Homebrew already installed at: ${brew_bin}"
    append_shellenv_if_missing "$brew_bin"
    return 0
  fi

  echo "Installing Homebrew..."
  /bin/bash -c "$(curl -fsSL "${BREW_INSTALL_URL}")"

  brew_bin="$(detect_brew_bin)"
  append_shellenv_if_missing "$brew_bin"
}

install_supabase_cli() {
  echo "Installing Supabase CLI via Homebrew..."
  brew install supabase/tap/supabase
}

main() {
  install_homebrew_if_needed

  echo "Homebrew version:"
  brew --version

  install_supabase_cli

  echo "Supabase CLI version:"
  supabase --version

  cat <<EOF

Next steps:
  cd ${REPO_ROOT}
  supabase start
  ORANGEDOT_SUPABASE_RUN_INTEGRATION=1 dotnet test OrangeDot.Supabase.sln --configuration Release
EOF
}

main "$@"
