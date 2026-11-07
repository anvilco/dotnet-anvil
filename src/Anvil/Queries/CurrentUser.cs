using System;

namespace Anvil.Queries
{
    /// <summary>The pre-1.0 holder of this operation; now the same document as <see cref="GraphQL.Operations"/>.</summary>
    [Obsolete("Use Anvil.GraphQL.Operations.")]
    public class CurrentUser : BaseQuery
    {
        public override string DefaultQuery => GraphQL.Operations.CurrentUserQuery(null);

        public override string? DefaultResponseQuery => GraphQL.Operations.CurrentUserDefaultResponseQuery;

        public override string GetFullDefaultQuery() => DefaultQuery;
    }
}
