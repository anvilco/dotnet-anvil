using System.Collections.Generic;

namespace Anvil.Payloads.Response
{
    public class FieldType
    {
        public string? Message { get; set; }
        public string? Property { get; set; }
    }

    public class Error
    {
        public string? Name { get; set; }
        public List<FieldType>? Fields { get; set; }
    }
}