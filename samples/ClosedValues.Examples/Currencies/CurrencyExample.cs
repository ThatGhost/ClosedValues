using System;

namespace ClosedValues.Examples.Currencies
{
    /// <summary>
    /// Shows a closed value with two members: behaviour that reads both, and equality across them.
    /// </summary>
    public sealed class CurrencyExample
    {
        public void Run()
        {
            Console.WriteLine("2. Currency: two members");

            const decimal amount = 1234.5678m;
            foreach (Currency currency in Currency.All)
            {
                Console.WriteLine($"   {currency.Code} ({currency.DecimalPlaces} decimals): {currency.Format(amount)}");
            }

            Currency fromSettings = Currency.All[1];
            Console.WriteLine($"   The second currency equals UsDollar: {fromSettings == Currency.UsDollar}.");
            Console.WriteLine();
        }
    }
}
