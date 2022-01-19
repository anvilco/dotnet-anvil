namespace Anvil.Queries
{
    public class GenerateEtchSignUrl : BaseQuery
    {
        public override string DefaultQuery => @"
        mutation GenerateEtchSignURL ($signerEid: String!, $clientUserId: String!)
        {
            generateEtchSignURL (signerEid: $signerEid, clientUserId: $clientUserId)
        }";

        public override string? DefaultResponseQuery { get; }
    }
}