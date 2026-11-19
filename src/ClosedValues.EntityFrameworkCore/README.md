# ClosedValues.EntityFrameworkCore

EF Core value converters for [ClosedValues](https://github.com/ThatGhost/ClosedValues) types. The package depends on
ClosedValues and brings it along.

Every closed value type gets a nested `ClosedValueEfCoreConverter`. It stores the key in a column of the key's type,
such as `"EUR"` or `3`, or the field name when the type has no key. On EF Core 6 or later, one extension method per
assembly registers the converter of every closed value type in that assembly:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.ConfigureClosedValues();
}
```

Queries such as `orders.Where(order => order.Currency == Currency.Euro)` translate to a comparison of the stored
value. To map one property only, use `HasConversion<Currency.ClosedValueEfCoreConverter>()`.

`ConfigureClosedValues` is internal to the assembly that declares the closed value types. A `DbContext` in another
assembly calls `Properties<T>().HaveConversion<T.ClosedValueEfCoreConverter>()` per type instead.

Turn the converters off for one type with `[ClosedValue(EfCore = false)]`, or for the whole project with
`<ClosedValuesEfCore>false</ClosedValuesEfCore>`. A setting on the type wins.
