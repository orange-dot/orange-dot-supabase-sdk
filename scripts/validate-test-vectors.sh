#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VECTORS_DIR="$REPO_ROOT/spec/test-vectors"
SCHEMAS_DIR="$VECTORS_DIR/schemas"
INVARIANTS_FILE="$REPO_ROOT/spec/invariants.md"

errors=0

# ── Tool check ───────────────────────────────────────────────────────────────

if ! command -v check-jsonschema &>/dev/null; then
  echo "ERROR: check-jsonschema not found on PATH."
  echo "Install with:  pip install check-jsonschema"
  exit 1
fi

if ! command -v jq &>/dev/null; then
  echo "ERROR: jq not found on PATH."
  exit 1
fi

# ── Schema validation ────────────────────────────────────────────────────────

for domain in auth lifecycle urls; do
  schema="$SCHEMAS_DIR/${domain}.schema.json"
  vector_dir="$VECTORS_DIR/$domain"

  if [ ! -d "$vector_dir" ] || [ -z "$(ls "$vector_dir"/*.json 2>/dev/null)" ]; then
    echo "WARN: no vectors found in $vector_dir"
    continue
  fi

  echo "Validating $domain vectors against $schema ..."
  if ! check-jsonschema --schemafile "$schema" "$vector_dir"/*.json; then
    errors=$((errors + 1))
  fi
done

# ── ID uniqueness ────────────────────────────────────────────────────────────

echo ""
echo "Checking ID uniqueness ..."

all_ids=$(find "$VECTORS_DIR" -name '*.json' ! -path '*/schemas/*' -exec jq -r '.id' {} +)
duplicates=$(echo "$all_ids" | sort | uniq -d)

if [ -n "$duplicates" ]; then
  echo "ERROR: Duplicate vector IDs found:"
  echo "$duplicates"
  errors=$((errors + 1))
else
  echo "  All vector IDs are unique."
fi

# ── Invariant cross-reference ────────────────────────────────────────────────

echo ""
echo "Checking invariant references ..."

# Extract valid invariant IDs from invariants.md
valid_invariants=$(grep -oE 'INV-[A-Z]{2,3}-[0-9]{3}' "$INVARIANTS_FILE" | sort -u)

# Extract referenced invariant IDs from all vectors
referenced_invariants=$(find "$VECTORS_DIR" -name '*.json' ! -path '*/schemas/*' \
  -exec jq -r '.invariants // [] | .[]' {} + | sort -u)

inv_errors=0
while IFS= read -r inv_id; do
  [ -z "$inv_id" ] && continue
  if ! echo "$valid_invariants" | grep -qxF "$inv_id"; then
    echo "ERROR: Unknown invariant '$inv_id' referenced in test vectors"
    inv_errors=1
  fi
done <<< "$referenced_invariants"

if [ "$inv_errors" -eq 0 ]; then
  echo "  All invariant references are valid."
else
  errors=$((errors + 1))
fi

# ── Summary ──────────────────────────────────────────────────────────────────

echo ""
if [ "$errors" -gt 0 ]; then
  echo "FAILED: $errors check(s) failed."
  exit 1
else
  echo "OK: All checks passed."
fi
