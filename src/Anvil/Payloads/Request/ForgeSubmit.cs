namespace Anvil.Payloads.Request
{
    public class ForgeSubmit
    {
        public string? ForgeEid { get; set; }
        public string? WeldDataEid { get; set; }
        public string? SubmissionEid { get; set; }
        public object? Payload { get; set; }
        public int? CurrentStep { get; set; }
        public bool? Complete { get; set; }
        public bool? IsTest { get; set; }
        public string? Timezone { get; set; }
        public string? GroupArrayId { get; set; }
        public int? GroupArrayIndex { get; set; }
        public string? ErrorType { get; set; }
    }
}