namespace Anvil.Queries
{
    public class CurrentUser : BaseQuery
    {
        public override string DefaultQuery => @"
            query currentUser {
                currentUser {
                    id
                    eid
                    email
                    name
                    firstName
                    lastName
                    role
                    verifiedEmail
                    createdAt
                    updatedAt
                    extra
                    numSentEtchPackets
                }
            }
        ";
        public override string? DefaultResponseQuery { get; }
    }
}