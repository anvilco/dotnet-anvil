// Prints every GraphQL operation the client sends, as a JSON object of
// operation name to document, for dev/check-graphql.mjs to validate against
// the schema in schema/anvil_schema.graphql.
using Anvil.GraphQL;

using Newtonsoft.Json;

var operations = new Dictionary<string, string>
{
    ["CreateEtchPacket"] = Operations.CreateEtchPacketMutation(),
    ["EtchPacket"] = Operations.EtchPacketQuery(),
    ["GenerateEtchSignURL"] = Operations.GenerateEtchSignUrlMutation(),
    ["ForgeSubmit"] = Operations.ForgeSubmitMutation(),
    ["RemoveWeldData"] = Operations.RemoveWeldDataMutation(),
    ["CurrentUser"] = Operations.CurrentUserQuery(),
};

Console.WriteLine(JsonConvert.SerializeObject(operations, Formatting.Indented));
