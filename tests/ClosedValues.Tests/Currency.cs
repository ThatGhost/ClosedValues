namespace ClosedValues.Tests
{
    /// <summary>
    /// Two members, with the code as the key. The validation hook normalizes the code.
    /// </summary>
    [ClosedValue]
    public sealed partial class Currency
    {
        [ClosedValueKey]
        public string Code { get; }

        public int Decimals { get; }

        public static readonly Currency Euro = new("eur", 2);
        public static readonly Currency Yen = new("JPY", 0);
        public static readonly Currency Dinar = new("KWD", 3);

        static partial void ValidateConstructorArguments(ref string code, ref int decimals)
        {
            if (decimals < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(decimals));
            }

            code = code.ToUpperInvariant();
        }
    }
}
