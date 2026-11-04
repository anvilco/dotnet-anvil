#!/usr/bin/env bash
# Refreshes the API specs in schema/:
#
# - schema/anvil_schema.graphql, from the published @anvilco/types npm package
#   (https://www.npmjs.com/package/@anvilco/types). It ships the Anvil GraphQL
#   schema, regenerated from production after every deploy. node-anvil and
#   python-anvil validate their operations against that copy, and so does this
#   client: using the same source keeps the clients in step.
# - schema/openapi.json, the API's own OpenAPI spec, from production.
#
# Neither needs an API key. On any failure the existing files are untouched.
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

cd "$TMP"
npm pack "$PACKAGE@$VERSION" --silent >/dev/null
tar -xzf anvilco-types-*.tgz
RESOLVED="$(node -p 'require("./package/package.json").version')"
test -f package/schema/anvil-schema.graphql

curl -sSf "$OPENAPI_URL" -o openapi.raw.json
# Pretty-printed with sorted keys so a diff only shows real changes
jq -S . openapi.raw.json > openapi.json
jq -e '.openapi and .paths' openapi.json >/dev/null

mkdir -p "$SCHEMA_DIR"
cp package/schema/anvil-schema.graphql "$SCHEMA_DIR/anvil_schema.graphql"
cp openapi.json "$SCHEMA_DIR/openapi.json"
echo "Wrote the GraphQL schema from $PACKAGE@$RESOLVED to schema/anvil_schema.graphql"
echo "Wrote the OpenAPI spec from $OPENAPI_URL to schema/openapi.json"
