namespace Anvil.Queries
{
    public class RemoveWeldData : BaseQuery
    {
        public override string DefaultQuery => @"
            mutation RemoveWeldData ($eid: String!) {
                removeWeldData (eid: $eid)
            }
        ";

        public override string? DefaultResponseQuery { get; }
    }
}