#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SPEC_DIR="$ROOT_DIR/spec/tla"
TOOLS_DIR="${TLA_TOOLS_DIR:-$HOME/tools/tla}"
TLA_VERSION="${TLA_VERSION:-1.7.4}"
TLA_JAR="${TLA_JAR:-$TOOLS_DIR/tla2tools.jar}"
TLA_URL="${TLA_URL:-https://github.com/tlaplus/tlaplus/releases/download/v${TLA_VERSION}/tla2tools.jar}"
MODEL_NAME="${1:-}"

if [[ -z "$MODEL_NAME" ]]; then
  echo "usage: bash scripts/run-tlc.sh <ModelBaseName> [tlc-args...]" >&2
  exit 1
fi

shift

MODULE="${MODEL_NAME}.tla"
CONFIG="${MODEL_NAME}.cfg"

if ! command -v java >/dev/null 2>&1; then
  echo "java was not found on PATH" >&2
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl was not found on PATH" >&2
  exit 1
fi

mkdir -p "$TOOLS_DIR"

if [[ ! -f "$TLA_JAR" ]]; then
  echo "Downloading tla2tools.jar to $TLA_JAR"
  curl -L --fail -o "$TLA_JAR" "$TLA_URL"
fi

if [[ ! -f "$SPEC_DIR/$MODULE" ]]; then
  echo "Missing TLA module: $SPEC_DIR/$MODULE" >&2
  exit 1
fi

if [[ ! -f "$SPEC_DIR/$CONFIG" ]]; then
  echo "Missing TLC config: $SPEC_DIR/$CONFIG" >&2
  exit 1
fi

cd "$SPEC_DIR"

echo "Running TLC for $MODULE with $CONFIG using TLA+ $TLA_VERSION"
exec java -cp "$TLA_JAR" tlc2.TLC "$MODULE" -config "$CONFIG" "$@"
