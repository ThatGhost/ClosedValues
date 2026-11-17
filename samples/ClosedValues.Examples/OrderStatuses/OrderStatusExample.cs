using System;

namespace ClosedValues.Examples.OrderStatuses
{
    /// <summary>
    /// Shows a closed value with one string member: parse it, print it, and switch on it.
    /// </summary>
    public sealed class OrderStatusExample
    {
        public void Run()
        {
            Console.WriteLine("1. Order status: one string member");

            if (OrderStatus.TryParse("SHIPPED", StringComparison.OrdinalIgnoreCase, out OrderStatus? status))
            {
                Console.WriteLine($"   Parsed 'SHIPPED' as '{status}'. Final: {status.IsFinal}.");
            }

            Console.WriteLine($"   'returned' is a status: {OrderStatus.TryParse("returned", out _)}.");

            foreach (OrderStatus each in OrderStatus.All)
            {
                string nextStep = each.Match(
                    pending: () => "wait for the payment",
                    paid: () => "pack and ship the order",
                    shipped: () => "track the parcel",
                    delivered: () => "ask for a review",
                    cancelled: () => "refund the customer");

                Console.WriteLine($"   {each,-10} -> {nextStep}");
            }

            Console.WriteLine();
        }
    }
}
