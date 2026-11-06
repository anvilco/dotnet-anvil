# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

- `RestClient` and `GraphQLClient` take an optional `baseUrl`, e.g. `new RestClient(apiKey, baseUrl)`. It defaults to production, `https://app.useanvil.com`, and every URL derives from it: REST requests go to `{baseUrl}/api/v1/`, GraphQL to `{baseUrl}/graphql` (no longer `https://graphql.useanvil.com`) and document downloads to `{baseUrl}/api/document-group/`.
- Add `test-client/`, a console app that runs every client method against a real environment: `ANVIL_API_KEY=... dotnet run --project test-client`.
- Add a dependency on the published `@anvilco/types` package, which ships the API's GraphQL schema and OpenAPI spec (`schema/openapi.json` holds the spec until the installed version ships it), and `dev/check-graphql.mjs`, which validates the client's queries and mutations against the schema.
- Line endings are LF, and `.gitattributes` keeps them that way.
- Update the test dependencies (xunit 2.9, Microsoft.NET.Test.Sdk 18, coverlet 10) and Newtonsoft.Json to 13.0.4. Moq was unused and is removed.

## [v0.6.0] - 2026-03-03

See the [GitHub release](https://github.com/anvilco/dotnet-anvil/releases/tag/v0.6.0). Earlier releases are on the [releases page](https://github.com/anvilco/dotnet-anvil/releases).
