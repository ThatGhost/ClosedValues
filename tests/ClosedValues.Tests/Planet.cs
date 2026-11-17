namespace ClosedValues.Tests
{
    [ClosedValue]
    public sealed partial class Planet
    {
        public string Name { get; }

        public int OrderFromSun { get; }

        public bool IsInner => OrderFromSun <= 4;

        public static readonly Planet Mercury = new("Mercury", 1);
        public static readonly Planet Venus = new("Venus", 2);
        public static readonly Planet Earth = new("Earth", 3);
        public static readonly Planet Mars = new("Mars", 4);
        public static readonly Planet Jupiter = new("Jupiter", 5);
    }
}
