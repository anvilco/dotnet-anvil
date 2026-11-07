using System;

namespace Anvil.Queries
{
    /// <summary>The pre-1.0 holder of this operation; now the same document as <see cref="GraphQL.Operations"/>.</summary>
    [Obsolete("Use Anvil.GraphQL.Operations.")]
    public class CreateEtchPacket : BaseQuery
    {
        public override string DefaultQuery => GraphQL.Operations.CreateEtchPacketMutation(null);

        public override string? DefaultResponseQuery => GraphQL.Operations.CreateEtchPacketDefaultResponseQuery;

        public override string GetFullDefaultQuery() => DefaultQuery;
    }
}
