using System;

namespace Anvil.Queries
{
    /// <summary>The pre-1.0 holder of this operation; now the same document as <see cref="GraphQL.Operations"/>.</summary>
    [Obsolete("Use Anvil.GraphQL.Operations.")]
    public class ForgeSubmit : BaseQuery
    {
        public override string DefaultQuery => GraphQL.Operations.ForgeSubmitMutation(null);

        public override string? DefaultResponseQuery => GraphQL.Operations.ForgeSubmitDefaultResponseQuery;

        public override string GetFullDefaultQuery() => DefaultQuery;
    }
}
