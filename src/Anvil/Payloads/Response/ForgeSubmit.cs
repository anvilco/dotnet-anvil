using System;

namespace Anvil.Payloads.Response
{
    public class ForgeSubmitPayload
    {
        public ForgeSubmitData? ForgeSubmit { get; set; }
        // public ForgeSubmitData ForgeSubmit { get; set; }
    }

    public class ForgeSubmitData
    {
        public int? Id { get; set; }
        public string? Eid { get; set; }
        public object? PayloadValue { get; set; }
        public bool? IsComplete { get; set; }
        public int? CurrentStep { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public object? Signer { get; set; }
        public object? WeldData { get; set; }
    }
}