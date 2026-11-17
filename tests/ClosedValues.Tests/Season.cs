namespace ClosedValues.Tests
{
    [ClosedValue]
    public sealed partial record Season
    {
        public string Name { get; }
        public int Order { get; }

        public static readonly Season Spring = new("Spring", 1);
        public static readonly Season Summer = new("Summer", 2);
        public static readonly Season Autumn = new("Autumn", 3);
        public static readonly Season Winter = new("Winter", 4);
    }
}
