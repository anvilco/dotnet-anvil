namespace Anvil.GraphQL
{
    /// <summary>
    /// The GraphQL operations the client sends: the same documents as
    /// node-anvil's <c>src/graphql/</c>, variable for variable. Each takes an
    /// optional response query, the selection of fields to return, with or
    /// without the surrounding braces; the default selections are the
    /// <c>*DefaultResponseQuery</c> constants. They are validated against the
    /// API schema in CI (<c>dev/check-graphql.mjs</c>).
    /// </summary>
    public static class Operations
    {
        public const string CreateEtchPacketDefaultResponseQuery = @"{
  eid
  name
  status
  isTest
  allowUpdates
  numberRemainingSigners
  detailsURL
  webhookURL
  completedAt
  archivedAt
  createdAt
  updatedAt
  documentGroup {
    eid
    status
    files
    signers {
      eid
      aliasId
      routingOrder
      name
      email
      status
      signActionType
    }
  }
}";

        /// <summary>The <c>createEtchPacket</c> mutation.</summary>
        public static string CreateEtchPacketMutation(string? responseQuery = null) => $@"
  mutation CreateEtchPacket(
    $name: String,
    $organizationEid: String,
    $files: [EtchFile!],
    $isDraft: Boolean,
    $isTest: Boolean,
    $signatureEmailSubject: String,
    $signatureEmailBody: String,
    $signatureProvider: String,
    $signaturePageOptions: JSON,
    $signers: [JSON!],
    $data: JSON,
    $webhookURL: String,
    $replyToName: String,
    $replyToEmail: String,
    $enableEmails: JSON,
    $createCastTemplatesFromUploads: Boolean,
    $duplicateCasts: Boolean,
    $mergePDFs: Boolean,
    $allowUpdates: Boolean
  ) {{
    createEtchPacket(
      name: $name,
      organizationEid: $organizationEid,
      files: $files,
      isDraft: $isDraft,
      isTest: $isTest,
      signatureEmailSubject: $signatureEmailSubject,
      signatureEmailBody: $signatureEmailBody,
      signatureProvider: $signatureProvider,
      signaturePageOptions: $signaturePageOptions,
      signers: $signers,
      data: $data,
      webhookURL: $webhookURL,
      replyToName: $replyToName,
      replyToEmail: $replyToEmail,
      enableEmails: $enableEmails,
      createCastTemplatesFromUploads: $createCastTemplatesFromUploads,
      duplicateCasts: $duplicateCasts,
      mergePDFs: $mergePDFs,
      allowUpdates: $allowUpdates
    ) {Selection(responseQuery ?? CreateEtchPacketDefaultResponseQuery)}
  }}";

        public const string EtchPacketDefaultResponseQuery = @"{
  id
  eid
  name
  status
  isTest
  numberRemainingSigners
  webhookURL
  detailsURL
  completedAt
  archivedAt
  createdAt
  updatedAt
  documentGroup {
    eid
    status
    files
    signers {
      id
      eid
      aliasId
      routingOrder
      name
      email
      status
      signActionType
    }
  }
}";

        /// <summary>The <c>etchPacket</c> query.</summary>
        public static string EtchPacketQuery(string? responseQuery = null) => $@"
  query EtchPacket (
    $eid: String!,
  ) {{
    etchPacket (
      eid: $eid,
    ) {Selection(responseQuery ?? EtchPacketDefaultResponseQuery)}
  }}
";

        public const string ForgeSubmitDefaultResponseQuery = @"{
  id
  eid
  status
  continueURL
  payloadValue
  currentStep
  completedAt
  createdAt
  updatedAt
  signer {
    name
    email
    status
    routingOrder
  }
  weldData {
    eid
    isTest
    isComplete
    agents
  }
}";

        /// <summary>The <c>forgeSubmit</c> mutation.</summary>
        public static string ForgeSubmitMutation(string? responseQuery = null) => $@"
  mutation ForgeSubmit(
    $forgeEid: String!,
    $weldDataEid: String,
    $submissionEid: String,
    $payload: JSON!,
    $enforcePayloadValidOnCreate: Boolean,
    $currentStep: Int,
    $complete: Boolean,
    $isTest: Boolean,
    $timezone: String,
    $webhookURL: String,
    $groupArrayId: String,
    $groupArrayIndex: Int
  ) {{
    forgeSubmit(
      forgeEid: $forgeEid,
      weldDataEid: $weldDataEid,
      submissionEid: $submissionEid,
      payload: $payload,
      enforcePayloadValidOnCreate: $enforcePayloadValidOnCreate,
      currentStep: $currentStep,
      complete: $complete,
      isTest: $isTest,
      timezone: $timezone,
      webhookURL: $webhookURL,
      groupArrayId: $groupArrayId,
      groupArrayIndex: $groupArrayIndex
    ) {Selection(responseQuery ?? ForgeSubmitDefaultResponseQuery)}
  }}";

        /// <summary>The <c>generateEtchSignURL</c> mutation, which returns the URL.</summary>
        public static string GenerateEtchSignUrlMutation() => @"
  mutation GenerateEtchSignURL (
    $signerEid: String!,
    $clientUserId: String!,
  ) {
    generateEtchSignURL (
      signerEid: $signerEid,
      clientUserId: $clientUserId
    )
  }
";

        /// <summary>The <c>removeWeldData</c> mutation, which returns whether the data was removed.</summary>
        public static string RemoveWeldDataMutation() => @"
  mutation RemoveWeldData (
    $eid: String!,
  ) {
    removeWeldData (
      eid: $eid,
    )
  }";

        public const string CurrentUserDefaultResponseQuery = @"{
  eid
  email
  name
  firstName
  lastName
  role
  verifiedEmail
  createdAt
  updatedAt
  organizations {
    eid
    name
    slug
  }
}";

        /// <summary>The <c>currentUser</c> query.</summary>
        public static string CurrentUserQuery(string? responseQuery = null) => $@"
  query CurrentUser {{
    currentUser {Selection(responseQuery ?? CurrentUserDefaultResponseQuery)}
  }}";

        /// <summary>A response query with its braces, whether or not it was given with them.</summary>
        public static string Selection(string responseQuery)
        {
            var trimmed = responseQuery.Trim();
            return trimmed.StartsWith("{") ? trimmed : "{ " + trimmed + " }";
        }
    }
}
