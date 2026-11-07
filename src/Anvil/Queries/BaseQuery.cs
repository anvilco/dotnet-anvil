using System;

namespace Anvil.Queries
{
    /// <summary>The pre-1.0 base of the operation holders. See <see cref="GraphQL.Operations"/>.</summary>
    [Obsolete("Use Anvil.GraphQL.Operations.")]
    public abstract class BaseQuery
    {
        public abstract string DefaultQuery { get; }
        public abstract string? DefaultResponseQuery { get; }

        public virtual string GetFullDefaultQuery()
        {
            return DefaultResponseQuery == null
                ? DefaultQuery
                : String.Format(DefaultQuery, DefaultResponseQuery);
        }
    }
}
