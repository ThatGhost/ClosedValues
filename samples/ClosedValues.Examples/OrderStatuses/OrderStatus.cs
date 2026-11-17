namespace ClosedValues.Examples.OrderStatuses
{
    /// <summary>
    /// The status of an order. One string member, so the generator adds <c>TryParse</c>, and <c>ToString</c> returns that string.
    /// </summary>
    [ClosedValue]
    public sealed partial class OrderStatus
    {
        public string Name { get; }

        public bool IsFinal => this == Delivered || this == Cancelled;

        public static readonly OrderStatus Pending = new("pending");
        public static readonly OrderStatus Paid = new("paid");
        public static readonly OrderStatus Shipped = new("shipped");
        public static readonly OrderStatus Delivered = new("delivered");
        public static readonly OrderStatus Cancelled = new("cancelled");
    }
}
