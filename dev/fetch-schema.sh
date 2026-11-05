#!/usr/bin/env bash
# Refreshes the API specs the generated types and the operation check come from:
#
# - The @anvilco/types dependency in dev/package.json, bumped to the latest
#   published version (https://www.npmjs.com/package/@anvilco/types), or the
#   one given. It ships the Anvil GraphQL schema (its `schema.graphql` export),
#   regenerated from production after every deploy. node-anvil validates its
#   operations against the same copy, and so does this client: one source keeps
#   the clients in step.
# - schema/openapi.json, the API's own OpenAPI spec, from production.
#
# Neither needs an API key. On any failure the existing files are untouched.
# Then run `npm run generate` in dev/ to regenerate src/Anvil/Types.
#
# Usage: dev/fetch-schema.sh [@anvilco/types version]   (defaults to latest)
set -euo pipefail

PACKAGE="@anvilco/types"
VERSION="${1:-latest}"
OPENAPI_URL="https://app.useanvil.com/api/v1/openapi.json"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCHEMA_DIR="$ROOT/schema"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# The OpenAPI spec first, so a failed fetch leaves package.json alone too
curl -sSf "$OPENAPI_URL" -o "$TMP/openapi.raw.json"
# Pretty-printed with sorted keys so a diff only shows real changes
jq -S . "$TMP/openapi.raw.json" > "$TMP/openapi.json"
jq -e '.openapi and .paths' "$TMP/openapi.json" >/dev/null

(cd "$ROOT/dev" && npm install --save-exact --no-audit --no-fund "$PACKAGE@$VERSION" >/dev/null)
RESOLVED="$(node -p "require('$ROOT/dev/node_modules/$PACKAGE/package.json').version")"

mkdir -p "$SCHEMA_DIR"
cp "$TMP/openapi.json" "$SCHEMA_DIR/openapi.json"
echo "dev/package.json depends on $PACKAGE@$RESOLVED, which ships the GraphQL schema"
echo "Wrote the OpenAPI spec from $OPENAPI_URL to schema/openapi.json"
