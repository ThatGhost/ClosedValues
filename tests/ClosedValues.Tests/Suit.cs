namespace ClosedValues.Tests
{
    [ClosedValue]
    public readonly partial struct Suit
    {
        public string Name { get; }
        public bool IsRed { get; }

        public static readonly Suit Clubs = new("Clubs", false);
        public static readonly Suit Diamonds = new("Diamonds", true);
        public static readonly Suit Hearts = new("Hearts", true);
        public static readonly Suit Spades = new("Spades", false);
    }
}
