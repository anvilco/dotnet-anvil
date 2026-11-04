namespace Anvil.Payloads
{
    /// <summary>
    /// The body of <see cref="AnvilClient.FillPdfAsync"/>: the data to fill the
    /// template with, and the options the API documents (generated from its
    /// OpenAPI spec in <see cref="Types.Rest.FillPdfRequest"/>).
    /// <c>Data</c> is a dictionary of field id to value, or a list of them for a
    /// multi-page fill; a value is a string, number, boolean or an object for a
    /// structured field, like <c>new { num = "5551231234", region = "US" }</c>.
    /// </summary>
    public class FillPdfPayload : Types.Rest.FillPdfRequest
    {
    }
}
