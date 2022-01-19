namespace Anvil.Queries
{
    public class GetEtchPacket : BaseQuery
    {
        public override string DefaultQuery => @"
            query GetEtchPacket($eid: String!) {{ 
                etchPacket(eid: $eid) {0}
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