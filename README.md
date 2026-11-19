# ClosedValues

ClosedValues is a source generator and analyzer for closed value types. A closed value type is a class,
record, struct or record struct with named members and a fixed set of static instances. Write the members
and the instances. The generator writes the rest, and the analyzer keeps the set closed.

```csharp
using ClosedValues;

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
```

## Generated members

| Member | Behaviour |
|---|---|
| Private constructor | One parameter per get-only auto-property, in declaration order. It first calls the partial method `ValidateConstructorArguments`, see below. |
| `All` | Every static readonly instance, in declaration order. |
| `FromName(name)`, `TryFromName(name, out result)` | Finds the instance by the name of its field, such as `"Earth"`. The default comparison is ordinal; an overload takes a `StringComparison`. `FromName` throws an `ArgumentException` for an unknown name. |
| `TryParse(value, out result)` | Only when the type has a key, see below. Finds the instance whose key equals `value`. A `string` key also gets an overload with a `StringComparison`; the default comparison is ordinal. |
| `Parse(value)` | Like `TryParse`, but throws an `ArgumentException` when no instance has the value. |
| Conversions | Under the same condition as `TryParse`: an implicit conversion to the key, and an explicit conversion from it that calls `Parse`. |
| `Match<TResult>(...)` | One `Func<TResult>` per instance. A new instance adds a parameter, so every call site stops compiling until it handles the new instance. |
| `Match<TResult>(@default, ...)` | A fallback `@default` and an optional `Func<TResult>` per instance. Handles a few instances and sends the rest to `@default`. This overload does not stop compiling for a new instance. When an instance is named `Default`, the fallback is `_` instead. |
| `Switch(...)`, `Switch(@default, ...)` | Like the two `Match` overloads, with an `Action` per instance instead of a function. |
| `Map<TResult>(...)` | One value per instance instead of a function: `planet.Map(mercury: 0, venus: 0, ...)`. Stops compiling for a new instance. |
| `Match<TState, TResult>(state, ...)` | Passes `state` to a `Func<TState, TResult>` per instance, so the functions can be `static` lambdas that capture nothing. |
| `Equals`, `GetHashCode`, `==`, `!=` | A class compares references: its instances are the only objects, so an instance equals only itself. A struct compares its get-only auto-properties. A record gets these from the compiler instead. |
| `CompareTo`, `<`, `<=`, `>`, `>=` | Only when the key is a basic type other than `bool` or `string`. Orders the instances by the key, and implements `IComparable<T>`. |
| `IParsable<T>` | When the project targets .NET 7 or later with C# 11 or later. Adds `Parse(string, IFormatProvider?)` and `TryParse(string?, IFormatProvider?, out T)`, which read the same text as the type converter. `T.Parse` then works in generic code, and ASP.NET Core minimal APIs bind the type from route and query values. `Parse` and `TryParse` are then reserved names on every closed value type. |
| `ToString` | Returns the first `string` member. The generator skips it when the type has no `string` member. |

Computed properties, such as `IsInner`, stay yours. The generator ignores them.

```csharp
int moons = planet.Match(
    mercury: () => 0,
    venus: () => 0,
    earth: () => 1,
    mars: () => 2,
    jupiter: () => 95);
```

```csharp
bool hasMoons = planet.Match(@default: () => true, mercury: () => false, venus: () => false);

Planet earth = Planet.FromName("Earth");
```

A `Match` parameter that is a C# keyword gets an `@` prefix: an instance `Default` becomes `@default`.

A get-only property of a delegate type gives each instance its own behaviour. A class compares references, so
the delegate takes no part in equality.

```csharp
public Func<decimal, decimal> Apply { get; }

public static readonly Discount None = new("none", amount => amount);
public static readonly Discount Half = new("half", amount => amount / 2);
```

## Key

The key identifies each instance. `TryParse`, `Parse`, the conversions, the ordering and the JSON and type
converters use it. Mark one get-only auto-property of a basic type with `[ClosedValueKey]`: `string`, `bool`,
`char`, a number type, `DateTime` or an enum. Without a marker, a type with exactly one member of a basic type
uses that member. A type without a key has no `TryParse` and `Parse`, and its converters write the field name.

```csharp
[ClosedValue]
public sealed partial class Currency
{
    [ClosedValueKey]
    public string Code { get; }

    public int Decimals { get; }

    public static readonly Currency Euro = new("EUR", 2);
    public static readonly Currency Yen = new("JPY", 0);
}
```

With a key, the stored value is the key, so renaming a field does not break data that is already stored.
[CV0010](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0010.md) reports two instances with the same constant key.

## Constructor validation

The generated constructor first calls a partial method with every argument by `ref`. Implement it to check or
change the arguments. Without a body, the compiler removes the call.

```csharp
static partial void ValidateConstructorArguments(ref string code, ref int decimals)
{
    if (decimals < 0) throw new ArgumentOutOfRangeException(nameof(decimals));
    code = code.ToUpperInvariant();
}
```

A class works directly with dependency injection: `services.AddSingleton(Planet.Earth)`.

## JSON and text conversion

The generator adds two nested converters and applies them to the type with attributes:

| Converter | Used by |
|---|---|
| `ClosedValueJsonConverter` | System.Text.Json, through `[JsonConverter]`. Also converts dictionary keys. |
| `ClosedValueTypeConverter` | `TypeDescriptor`, through `[TypeConverter]`: ASP.NET Core model binding of route and query values, configuration binding, and property grids. |

Both write one value per instance:

- The key, when the type has a key that is not an enum. `Priority.High` becomes `3`, `Visibility.Public`
  becomes `"public"` and `Currency.Euro` becomes `"EUR"`.
- The field name otherwise. `Planet.Earth` becomes `"Earth"`.

Reading an unknown value throws a `JsonException` or a `FormatException`. Text uses the invariant culture.
The generator skips an attribute when the type already has its own `[JsonConverter]` or `[TypeConverter]`,
and it skips the JSON converter when the project does not reference System.Text.Json.

System.Text.Json source generation (a `JsonSerializerContext`, as used for Native AOT) does not work with closed
value types. Source generators cannot see each other's output, so the JSON generator sees neither the generated
constructor nor the generated converter, and it fails to compile metadata for the type. Use the default
reflection-based serializer, or keep closed values out of the types that a `JsonSerializerContext` covers.

## EF Core

EF Core support is a separate package, `ClosedValues.EntityFrameworkCore`. It depends on ClosedValues, so it is the
only package an EF Core project needs:

```bash
dotnet add package ClosedValues.EntityFrameworkCore
```

With it, every type gets a nested `ClosedValueEfCoreConverter`. It stores the key in
a column of the key's type, such as `"EUR"` or `3`, or the field name when the type has no key. On EF Core 6 or
later, the generator also writes one extension method per assembly that registers the converter of every closed
value type in that assembly:

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

## Switching integrations off

Each integration that the generator adds can be turned off, for one type on the attribute or for the whole
project with an MSBuild property. A setting on the attribute wins over the project setting.

| Integration | On the type | In the project file |
|---|---|---|
| System.Text.Json converter | `[ClosedValue(Json = false)]` | `<ClosedValuesJson>false</ClosedValuesJson>` |
| `[TypeConverter]` attribute | `[ClosedValue(TypeConverter = false)]` | `<ClosedValuesTypeConverter>false</ClosedValuesTypeConverter>` |
| `IParsable<T>` | `[ClosedValue(Parsable = false)]` | `<ClosedValuesParsable>false</ClosedValuesParsable>` |
| EF Core converter, from `ClosedValues.EntityFrameworkCore` | `[ClosedValue(EfCore = false)]` | `<ClosedValuesEfCore>false</ClosedValuesEfCore>` |

`TypeConverter = false` removes the `[TypeConverter]` attribute, so `TypeDescriptor`, model binding and
configuration binding no longer use the converter. The nested `ClosedValueTypeConverter` class stays, because
the other converters read and write text through it. `Parsable = false` also frees the names `Parse` and
`TryParse` on a type without a key.

## Records and structs

`[ClosedValue]` also works on a sealed record, a struct and a record struct. The rules for the members are the
same: get-only auto-properties, no own constructor, and `partial`.

```csharp
[ClosedValue]
public readonly partial record struct Coin
{
    public int Cents { get; }

    public static readonly Coin Penny = new(1);
    public static readonly Coin Nickel = new(5);
    public static readonly Coin Dime = new(10);
}
```

| Kind | Differences |
|---|---|
| Record | Keeps the equality and `==` of the compiler. `Match` compares values, so a copy made with `with { }` still matches its instance. A record must be `sealed` and must not be positional. |
| Struct | The generator writes value equality and `IEquatable<T>`. `TryParse` returns the default value when nothing matches. |
| Record struct | Keeps the equality of the compiler, like a record. Behaves like a struct otherwise. |

A struct always has a default value, with every member set to its own default. That value is none of the
instances: `Match` throws for it. The analyzer reports `default` and `new()` outside the type
([CV0006](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0006.md), [CV0001](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0001.md)), but an array element or an unassigned field
still holds the default value. Choose a struct when the values are small and the default value is not a
risk, and a class otherwise.

## Examples

The project `samples/ClosedValues.Examples` is a console application with three examples:

| Example | Shows |
|---|---|
| `OrderStatus` | One string member: `TryParse`, `FromName`, the conversions, `Match` with and without a fallback. |
| `Currency` | Two members with the code as `[ClosedValueKey]`: a `Format` method that reads both, `Parse` by code, and JSON. |
| `SubscriptionPlan` | Four members: computed properties, queries over `All`, a dictionary key, an exhaustive `Match`, and the plan injected into a service through dependency injection. |

```bash
dotnet run --project samples/ClosedValues.Examples
```

## Rules

| Rule | Reports |
|---|---|
| [CV0001](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0001.md) | A `new` of the type outside its own body, including `new()` on a struct. |
| [CV0002](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0002.md) | An own instance constructor or a primary constructor. |
| [CV0003](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0003.md) | A type that is not `partial`. |
| [CV0004](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0004.md) | A class or record that is not `sealed`. |
| [CV0005](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0005.md) | A settable or init-only auto-property, or an instance field. |
| [CV0006](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0006.md) | The `default` value of a struct outside its own body. |
| [CV0007](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0007.md) | A nested, generic, static or `ref struct` type. |
| [CV0008](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0008.md) | A member with the name of a generated member, such as `All` or `Match`. |
| [CV0009](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0009.md) | A `[ClosedValueKey]` on more than one property, on a property that is not a get-only auto-property, or on a type that is not basic. |
| [CV0010](https://github.com/ThatGhost/ClosedValues/blob/main/docs/rules/CV0010.md) | Two instances with the same constant key. |

All rules are errors by default.

## Requirements

- A compiler with Roslyn 4.14 or later: the .NET SDK 9.0.300, or Visual Studio 2022 17.14. The generator
  needs this version to mark its attribute as embedded, so that projects with `InternalsVisibleTo` do not
  see two copies of it.
- C# 8 or later for nullable annotations in the generated code. Earlier versions get the same members
  without annotations.

## Build

```bash
dotnet build ClosedValues.slnx
```

```bash
tests/ClosedValues.Tests/bin/Debug/net10.0/ClosedValues.Tests.exe
```

```bash
dotnet pack src/ClosedValues/ClosedValues.csproj -c Release -o artifacts
```

```bash
dotnet pack src/ClosedValues.EntityFrameworkCore/ClosedValues.EntityFrameworkCore.csproj -c Release -o artifacts
```
