using System;

namespace Anvil.Queries
{
    /// <summary>The pre-1.0 holder of this operation; now the same document as <see cref="GraphQL.Operations"/>.</summary>
    [Obsolete("Use Anvil.GraphQL.Operations.")]
    public class GetEtchPacket : BaseQuery
    {
        public override string DefaultQuery => GraphQL.Operations.EtchPacketQuery(null);

        public override string? DefaultResponseQuery => GraphQL.Operations.EtchPacketDefaultResponseQuery;

        public override string GetFullDefaultQuery() => DefaultQuery;
    }
}
