using System;

namespace Anvil.Queries
{
    public abstract class BaseQuery
    {
        // NOTE: Be sure to escape each literaly `{` or `}` in `DefaultQuery`
        // if it is going through the `String.Format` method.
        public abstract string DefaultQuery { get; }
        public abstract string? DefaultResponseQuery { get; }

        public string GetFullDefaultQuery()
        {
            return DefaultResponseQuery == null
                ? DefaultQuery
                : String.Format(DefaultQuery, DefaultResponseQuery);
        }
    }
}