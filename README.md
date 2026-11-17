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
| Private constructor | One parameter per get-only auto-property, in declaration order. |
| `All` | Every static readonly instance, in declaration order. |
| `TryParse(value, out result)` | Only when the type has exactly one member of a basic type: `string`, `bool`, `char`, a number type, `DateTime` or an enum. Finds the instance whose member equals `value`. A `string` member also gets an overload with a `StringComparison`; the default comparison is ordinal. |
| `Match<TResult>(...)` | One `Func<TResult>` per instance. A new instance adds a parameter, so every call site stops compiling until it handles the new instance. |
| `Equals`, `GetHashCode`, `==`, `!=` | Compare the get-only auto-properties. A record gets these from the compiler instead. |
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

A `Match` parameter that is a C# keyword gets an `@` prefix: an instance `Default` becomes `@default`.

A class works directly with dependency injection: `services.AddSingleton(Planet.Earth)`.

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
| `OrderStatus` | One string member: parse a name with `All`, print the status, and switch on it with `Match`. |
| `Currency` | Two members: an ISO 4217 code and its decimal places, and a `Format` method that reads both. |
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
