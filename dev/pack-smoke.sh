#!/usr/bin/env bash
# Packs the library and installs the package into a scratch console app, so
# what gets published is exercised, not just the project reference: the
# package has both target frameworks, the README and the XML docs, and an app
# that references it can construct a client and send a request.
#
# Usage: dev/pack-smoke.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/pack-smoke"
FEED="$OUT/feed"
APP="$OUT/app"

rm -rf "$OUT"
mkdir -p "$FEED"

echo "Packing..."
dotnet pack "$ROOT/src/Anvil/Anvil.csproj" -c Release -o "$FEED" --nologo -v quiet
VERSION="$(dotnet msbuild "$ROOT/src/Anvil/Anvil.csproj" -getProperty:Version)"
PACKAGE="$FEED/Anvil.$VERSION.nupkg"
test -f "$PACKAGE" || { echo "::error::$PACKAGE was not produced"; exit 1; }
test -f "$FEED/Anvil.$VERSION.snupkg" || { echo "::error::No symbols package"; exit 1; }

echo "Checking the package contents..."
CONTENTS="$(unzip -l "$PACKAGE")"
for entry in lib/netstandard2.0/Anvil.dll lib/net10.0/Anvil.dll lib/netstandard2.0/Anvil.xml lib/net10.0/Anvil.xml README.md images/icon.png; do
  echo "$CONTENTS" | grep -q " $entry\$" || { echo "::error::$entry is not in the package"; exit 1; }
done

echo "Installing it into a scratch app..."
dotnet new console --output "$APP" --framework net10.0 --no-restore >/dev/null
cat > "$APP/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF
cat > "$APP/Program.cs" <<'EOF'
using System.Net.Http;

using Anvil;

// A port nothing listens on: the request must fail to connect, which proves
// the package's assembly loaded and the client sent a request.
using var anvil = new AnvilClient(new AnvilClientOptions
{
    ApiKey = "smoke-test",
    BaseUrl = "http://127.0.0.1:9",
    MaxRetries = 0,
    Timeout = TimeSpan.FromSeconds(10),
});

try
{
    await anvil.RequestGraphQLAsync("{ currentUser { eid } }");
    Console.Error.WriteLine("Expected the request to fail to connect");
    return 1;
}
catch (HttpRequestException)
{
    Console.WriteLine($"Anvil {AnvilClient.Version} loaded from the package and sent a request");
    return 0;
}
EOF
dotnet add "$APP" package Anvil --version "$VERSION" >/dev/null
dotnet run --project "$APP"
