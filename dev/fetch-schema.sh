#!/usr/bin/env bash
# Refreshes the API specs the generated types and the operation check come
# from: the @anvilco/types dependency in dev/package.json, bumped to the latest
# published version (https://www.npmjs.com/package/@anvilco/types), or the one
# given.
#
# The package ships the Anvil GraphQL schema (`@anvilco/types/schema.graphql`)
# and, in recent versions, the OpenAPI spec (`@anvilco/types/openapi.json`),
# both regenerated from production after every deploy. node-anvil and
# python-anvil generate from the same copy, so every client describes the same
# API build. A version that predates the spec shipping in the package falls
# back to fetching schema/openapi.json from production.
#
# Needs no API key. Then run `npm run generate` in dev/ to regenerate
# src/Anvil/Types.
#
# Usage: dev/fetch-schema.sh [@anvilco/types version]   (defaults to latest)
set -euo pipefail

PACKAGE="@anvilco/types"
VERSION="${1:-latest}"
OPENAPI_URL="https://app.useanvil.com/api/v1/openapi.json"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCHEMA_DIR="$ROOT/schema"
INSTALLED="$ROOT/dev/node_modules/$PACKAGE"

(cd "$ROOT/dev" && npm install --save-exact --no-audit --no-fund "$PACKAGE@$VERSION" >/dev/null)
RESOLVED="$(node -p "require('$INSTALLED/package.json').version")"
echo "dev/package.json depends on $PACKAGE@$RESOLVED, which ships the GraphQL schema"

if [ -f "$INSTALLED/schema/openapi.json" ]; then
  echo "It ships the OpenAPI spec too"
  if [ -f "$SCHEMA_DIR/openapi.json" ]; then
    rm "$SCHEMA_DIR/openapi.json"
    rmdir "$SCHEMA_DIR" 2>/dev/null || true
    echo "Removed schema/openapi.json: the package's copy is used instead"
  fi
else
  TMP="$(mktemp -d)"
  trap 'rm -rf "$TMP"' EXIT
  curl -sSf "$OPENAPI_URL" -o "$TMP/openapi.raw.json"
  # Pretty-printed with sorted keys so a diff only shows real changes
  jq -S . "$TMP/openapi.raw.json" > "$TMP/openapi.json"
  jq -e '.openapi and .paths' "$TMP/openapi.json" >/dev/null
  mkdir -p "$SCHEMA_DIR"
  cp "$TMP/openapi.json" "$SCHEMA_DIR/openapi.json"
  echo "It doesn't ship the OpenAPI spec yet: wrote it from $OPENAPI_URL to schema/openapi.json"
fi
