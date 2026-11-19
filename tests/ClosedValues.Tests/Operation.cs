using System;

namespace ClosedValues.Tests
{
    /// <summary>
    /// Behaviour per instance through a delegate member, and two instances with equal members.
    /// </summary>
    [ClosedValue]
    public sealed partial class Operation
    {
        public string Symbol { get; }

        public Func<int, int, int> Apply { get; }

        public static readonly Operation Add = new("+", (left, right) => left + right);
        public static readonly Operation Multiply = new("*", (left, right) => left * right);
        public static readonly Operation Plus = new("+", (left, right) => left + right);
    }
}
