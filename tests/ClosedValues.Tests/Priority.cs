namespace ClosedValues.Tests
{
    [ClosedValue]
    public sealed partial class Priority
    {
        public int Level { get; }

        public static readonly Priority Low = new(1);
        public static readonly Priority Medium = new(2);
        public static readonly Priority High = new(3);
    }
}
