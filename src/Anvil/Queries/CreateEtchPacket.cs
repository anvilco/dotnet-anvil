namespace Anvil.Queries
{
    public class CreateEtchPacket : BaseQuery
    {
        public override string DefaultQuery => @"
            mutation CreateEtchPacket (
                $name: String,
                $files: [EtchFile!],
                $isDraft: Boolean,
                $isTest: Boolean,
                $signatureEmailSubject: String,
                $signatureEmailBody: String,
                $signatureProvider: String,
                $signaturePageOptions: JSON,
                $replyToName: String,
                $replyToEmail: String,
                $signers: [JSON!],
                $webhookURL: String,
                $data: JSON,
            ) {{
                createEtchPacket (
                    name: $name,
                    files: $files,
                    isDraft: $isDraft,
                    isTest: $isTest,
                    signatureEmailSubject: $signatureEmailSubject,
                    signatureEmailBody: $signatureEmailBody,
                    signatureProvider: $signatureProvider,
                    signaturePageOptions: $signaturePageOptions,
                    signers: $signers,
                    replyToName: $replyToName,
                    replyToEmail: $replyToEmail,
                    webhookURL: $webhookURL,
                    data: $data
                )
                {0}
            }}
        ";

        public override string DefaultResponseQuery => @"{
          id
          eid
          name
          detailsURL
          documentGroup {
            id
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
    }
}