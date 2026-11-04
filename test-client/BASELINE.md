# test-client baseline on v0.6.0 (2026-11-04)

The output of `dotnet run --project test-client` against https://staging.useanvil.com on the 0.6.0 API, before the rewrite, for later phases to diff against. Checks marked `SKIP` with a phase are ones 0.6.0 cannot pass. The `FAIL`s are real: `currentUser` selects `numSentEtchPackets`, which the API no longer has, and two fills got a `429` because 0.6.0 has no rate limiter (a first run, a minute earlier, passed them). The operations the schema check flags (`documentGroup.id`, `forgeSubmit`'s `errorType`) still succeed against the API itself.

```
Testing the Anvil .NET client against https://staging.useanvil.com
  PASS  SendQuery (organization 24CaLWzZE7awEQlkfiaJ)
  SKIP  rate limiting: Phase 2: the client has no rate limiter yet
  SKIP  OAuth access token: Set ANVIL_ACCESS_TOKEN to test Bearer auth
  PASS  SendPostRequest (14504 bytes)
  PASS  GeneratePdf (markdown) (15008 bytes)
  PASS  GeneratePdf (HTML) (11079 bytes)
  PASS  FillPdf (PlYoZzxIxR2dZJClH2mc, 146691 bytes)
  FAIL  FillPdf (VERSION_LATEST): Error: TooManyRequests (RawContent: Rate limit exceeded, retry in 370 ms.)
  FAIL  FillPdf (stream): Error: TooManyRequests (RawContent: Rate limit exceeded, retry in 274 ms.)
  SKIP  CreateEtchPacket (multipart upload): Phase 4: multipart GraphQL file uploads are not supported yet (#26)
  PASS  CreateEtchPacket (base64 upload) (DEds8OY7QM8jcu6c0Z2E)
  SKIP  CreateEtchPacket (in-person signer): Phase 3: EtchSigner rejects the `in-person` signer type (#27)
  SKIP  SendQuery (createCast upload): Phase 4: multipart GraphQL file uploads are not supported yet (#26)
  PASS  GetEtchPacket (sent)
  PASS  GenerateEtchSignUrl (got a URL)
  PASS  DownloadDocuments (13405 bytes)
  PASS  ForgeSubmit (lYLjvuwTl3OTavnMauXN, created VgAfipq0Cxp5908EdpMU)
  PASS  RemoveWeldData (removed VgAfipq0Cxp5908EdpMU)
  FAIL  GetCurrentUser: Cannot query field "numSentEtchPackets" on type "User". (Message1: Cannot query field "numSentEtchPackets" on type "User".)
11 passed, 3 failed, 5 skipped
```
