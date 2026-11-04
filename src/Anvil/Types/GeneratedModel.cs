using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Anvil.Types
{
    /// <summary>
    /// Base class of the types generated from the API's schemas in
    /// <c>Anvil.Types.Rest</c>, <c>Anvil.Types.GraphQL</c> and
    /// <c>Anvil.Types.Operations</c>. Keeps any JSON members the type doesn't
    /// declare, so a field the API adds later, or one selected with a custom
    /// response query, is not lost.
    /// </summary>
    public abstract class GeneratedModel
    {
        /// <summary>JSON members of the object that the type has no property for.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? AdditionalData { get; set; }
    }
}
