namespace Anvil.Payloads.Request.Types
{
    public class Base64FileUpload
    {
        public string? Data { get; set; }
        public string? Filename { get; set; }
        public string? Mimetype { get; set; } = "application/pdf";
    }
}