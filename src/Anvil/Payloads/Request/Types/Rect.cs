// Required to use `JSONProperty`. We don't want the built-in .NET version.
using Newtonsoft.Json;

namespace Anvil.Payloads.Request.Types
{
    public class Rect
    {
        [JsonProperty("x")]
        public float? X { get; set; }
        [JsonProperty("y")]
        public float? Y { get; set; }
        [JsonProperty("width")]
        public float? Width { get; set; }
        [JsonProperty("height")]
        public float? Height { get; set; }
    }
}