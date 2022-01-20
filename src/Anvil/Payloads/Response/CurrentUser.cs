using Newtonsoft.Json.Linq;

namespace Anvil.Payloads.Response
{
    public class CurrentUserPayload
    {
        public JObject? CurrentUser { get; set; }
    }
}