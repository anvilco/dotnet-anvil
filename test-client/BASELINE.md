# test-client baseline on v0.6.0 (2026-11-06)

The output of `dotnet run --project test-client` against the API on 0.6.0, before the rewrite, for later phases to diff against. Checks marked `SKIP` with a phase are ones 0.6.0 cannot pass. The `FAIL` is real: `currentUser` selects `numSentEtchPackets`, which the API no longer has. 0.6.0 has no rate limiter, so a run soon after another can also get a `429` on the fills. The operations the schema check flags (`documentGroup.id`, `forgeSubmit`'s `errorType`) still succeed against the API itself.

```
Testing the Anvil .NET client against <the environment>
  PASS  SendQuery (organization 24CaLWzZE7awEQlkfiaJ)
  SKIP  rate limiting: Phase 2: the client has no rate limiter yet
  SKIP  OAuth access token: Set ANVIL_ACCESS_TOKEN to test Bearer auth
  PASS  SendPostRequest (14590 bytes)
  PASS  GeneratePdf (markdown) (15085 bytes)
  PASS  GeneratePdf (HTML) (11079 bytes)
  PASS  FillPdf (PlYoZzxIxR2dZJClH2mc, 146611 bytes)
  PASS  FillPdf (VERSION_LATEST) (146611 bytes)
  PASS  FillPdf (stream) (146611 bytes)
  SKIP  CreateEtchPacket (multipart upload): Phase 4: multipart GraphQL file uploads are not supported yet (#26)
  PASS  CreateEtchPacket (base64 upload) (mqELstpmIkhK3EgqUxyv)
  SKIP  CreateEtchPacket (in-person signer): Phase 3: EtchSigner rejects the `in-person` signer type (#27)
  SKIP  SendQuery (createCast upload): Phase 4: multipart GraphQL file uploads are not supported yet (#26)
  PASS  GetEtchPacket (sent)
  PASS  GenerateEtchSignUrl (got a URL)
  PASS  DownloadDocuments (13405 bytes)
  PASS  ForgeSubmit (lYLjvuwTl3OTavnMauXN, created l0xWFaFpPcw2U3ypBcir)
  PASS  RemoveWeldData (removed l0xWFaFpPcw2U3ypBcir)
  FAIL  GetCurrentUser: Cannot query field "numSentEtchPackets" on type "User". (Message1: Cannot query field "numSentEtchPackets" on type "User".)
13 passed, 1 failed, 5 skipped
```
