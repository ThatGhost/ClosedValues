namespace ClosedValues.Tests
{
    [ClosedValue]
    public sealed partial class Visibility
    {
        public string Keyword { get; }

        public static readonly Visibility Default = new("default");
        public static readonly Visibility Public = new("public");
        public static readonly Visibility Private = new("private");
    }
}
