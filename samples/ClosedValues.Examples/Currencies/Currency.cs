using System;
using System.Globalization;

namespace ClosedValues.Examples.Currencies
{
    /// <summary>
    /// A currency with its ISO 4217 code and the number of decimals it uses. The code is the key, so Parse, the
    /// conversions and JSON use it.
    /// </summary>
    [ClosedValue]
    public sealed partial class Currency
    {
        [ClosedValueKey]
        public string Code { get; }

        public int DecimalPlaces { get; }

        public static readonly Currency Euro = new("EUR", 2);
        public static readonly Currency UsDollar = new("USD", 2);
        public static readonly Currency JapaneseYen = new("JPY", 0);
        public static readonly Currency KuwaitiDinar = new("KWD", 3);

        /// <summary>
        /// Rounds the amount to the decimals of this currency and appends the code.
        /// </summary>
        public string Format(decimal amount)
        {
            decimal rounded = Math.Round(amount, DecimalPlaces, MidpointRounding.ToEven);
            return rounded.ToString("N" + DecimalPlaces, CultureInfo.InvariantCulture) + " " + Code;
        }
    }
}
