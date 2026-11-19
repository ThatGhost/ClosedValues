using System;

namespace ClosedValues.Examples.Currencies
{
    /// <summary>
    /// Shows a closed value with two members and a key: behaviour that reads both, and parsing by the key.
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

            Currency fromSettings = Currency.Parse("USD");
            Console.WriteLine($"   Parse(\"USD\") is UsDollar: {fromSettings == Currency.UsDollar}. As JSON: {System.Text.Json.JsonSerializer.Serialize(Currency.JapaneseYen)}.");
            Console.WriteLine();
        }
    }
}
