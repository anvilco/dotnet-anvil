// Prints every GraphQL operation the client sends, as a JSON object of
// operation name to document, for dev/check-graphql.mjs to validate against
// the schema that ships with @anvilco/types.
using Anvil.Queries;

using Newtonsoft.Json;

var operations = new Dictionary<string, string>
{
    ["CreateEtchPacket"] = new CreateEtchPacket().GetFullDefaultQuery(),
    ["GetEtchPacket"] = new GetEtchPacket().GetFullDefaultQuery(),
    ["GenerateEtchSignUrl"] = new GenerateEtchSignUrl().GetFullDefaultQuery(),
    ["ForgeSubmit"] = new ForgeSubmit().GetFullDefaultQuery(),
    ["RemoveWeldData"] = new RemoveWeldData().GetFullDefaultQuery(),
    ["CurrentUser"] = new CurrentUser().GetFullDefaultQuery(),
};

Console.WriteLine(JsonConvert.SerializeObject(operations, Formatting.Indented));
