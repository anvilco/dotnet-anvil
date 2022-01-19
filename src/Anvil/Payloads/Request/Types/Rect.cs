using System.Text.Json.Serialization;

namespace Anvil.Payloads.Request.Types
{
    public class Rect
    {
        [JsonPropertyName("x")]
        public float? X { get; set; }
        [JsonPropertyName("y")]
        public float? Y { get; set; }
        [JsonPropertyName("width")]
        public float? Width { get; set; }
        [JsonPropertyName("height")]
        public float? Height { get; set; }
    }
}