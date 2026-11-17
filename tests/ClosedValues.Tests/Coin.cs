namespace ClosedValues.Tests
{
    [ClosedValue]
    public readonly partial record struct Coin
    {
        public int Cents { get; }

        public static readonly Coin Penny = new(1);
        public static readonly Coin Nickel = new(5);
        public static readonly Coin Dime = new(10);
        public static readonly Coin Quarter = new(25);
    }
}
