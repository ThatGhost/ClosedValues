using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ClosedValues.Tests
{
    public sealed class KeySwitchParsableTests
    {
        private readonly SnippetCompiler compiler = new();

        [Fact]
        public void Key_IsUsedByParseAndTheConversions()
        {
            Assert.Same(Currency.Yen, Currency.Parse("JPY"));
            Assert.True(Currency.TryParse("kwd", StringComparison.OrdinalIgnoreCase, out Currency? dinar));
            Assert.Same(Currency.Dinar, dinar);
            Assert.Same(Currency.Dinar, (Currency)"KWD");

            string code = Currency.Euro;
            Assert.Equal("EUR", code);
        }

        [Fact]
        public void Key_IsUsedByTheConverters()
        {
            Assert.Equal("\"JPY\"", JsonSerializer.Serialize(Currency.Yen));
            Assert.Same(Currency.Euro, JsonSerializer.Deserialize<Currency>("\"EUR\""));
            Assert.Equal("KWD", TypeDescriptor.GetConverter(typeof(Currency)).ConvertToInvariantString(Currency.Dinar));
        }

        [Fact]
        public void ValidationHook_ChangesTheArguments()
        {
            Assert.Equal("EUR", Currency.Euro.Code);
            Assert.Equal(2, Currency.Euro.Decimals);
        }

        [Fact]
        public void Class_EqualityIsReferenceEquality()
        {
            Assert.Equal(Operation.Add.Symbol, Operation.Plus.Symbol);
            Assert.False(Operation.Add.Equals(Operation.Plus));
            Assert.False(Operation.Add == Operation.Plus);
            Assert.True(Operation.Add == Operation.All[0]);
            Assert.Equal("plus", Operation.Plus.Match(add: () => "add", multiply: () => "multiply", plus: () => "plus"));
        }

        [Fact]
        public void DelegateMember_GivesBehaviourPerInstance()
        {
            Assert.Equal(7, Operation.Add.Apply(3, 4));
            Assert.Equal(12, Operation.Multiply.Apply(3, 4));
        }

        [Fact]
        public void Switch_RunsTheActionOfTheInstance()
        {
            List<string> log = [];

            Planet.Mars.Switch(
                mercury: () => log.Add("mercury"),
                venus: () => log.Add("venus"),
                earth: () => log.Add("earth"),
                mars: () => log.Add("mars"),
                jupiter: () => log.Add("jupiter"));
            Suit.Hearts.Switch(@default: () => log.Add("other"), spades: () => log.Add("spades"));
            Season.Winter.Switch(@default: () => log.Add("other"), winter: () => log.Add("winter"));

            Assert.Equal(["mars", "other", "winter"], log);
        }

        [Fact]
        public void Map_ReturnsTheValueOfTheInstance()
        {
            Assert.Equal(95, Planet.Jupiter.Map(mercury: 0, venus: 0, earth: 1, mars: 2, jupiter: 95));
            Assert.Equal('h', Suit.Hearts.Map(clubs: 'c', diamonds: 'd', hearts: 'h', spades: 's'));
        }

        [Fact]
        public void MatchWithState_PassesTheState()
        {
            int moons = Planet.Earth.Match(
                10,
                static factor => 0,
                static factor => 0,
                static factor => factor * 1,
                static factor => factor * 2,
                static factor => factor * 95);

            Assert.Equal(10, moons);
        }

        [Fact]
        public void Parsable_ParsesTheKeyOrTheFieldName()
        {
            Assert.Same(Planet.Earth, ParseVia<Planet>("Earth"));
            Assert.Same(Priority.Medium, ParseVia<Priority>("2"));
            Assert.Same(Currency.Yen, ParseVia<Currency>("JPY"));
            Assert.Equal(Suit.Clubs, ParseVia<Suit>("Clubs"));
            Assert.Equal(Coin.Dime, ParseVia<Coin>("10"));
        }

        [Fact]
        public void Parsable_TryParseReturnsFalseForUnknownText()
        {
            Assert.False(TryParseVia<Planet>("Pluto", out Planet? planet));
            Assert.Null(planet);
            Assert.Throws<FormatException>(() => ParseVia<Priority>("nine"));
        }

        [Fact]
        public async Task KeyOfAnotherOrderedType_GivesOrdering()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Rank
                {
                    public string Name { get; }
                    [ClosedValueKey] public int Level { get; }
                    public static readonly Rank Low = new("Low", 1);
                    public static readonly Rank High = new("High", 2);
                    public static bool Less() => Low < High && Rank.Parse(2) == High && (int)Low == 1;
                }
                """);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Theory]
        [InlineData("[ClosedValueKey] public string Name { get; } [ClosedValueKey] public int Code { get; }", "new(\"Red\", 1)")]
        [InlineData("[ClosedValueKey] public int[] Codes { get; } public string Name { get; }", "new(new[] { 1 }, \"Red\")")]
        [InlineData("public string Name { get; } [ClosedValueKey] public int Code => 1;", "new(\"Red\")")]
        public async Task InvalidKey_ReportsCV0009(string members, string creation)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    {{members}}
                    public static readonly Color Red = {{creation}};
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0009");
        }

        [Theory]
        [InlineData("[ClosedValueKey] public string Code { get; } public int Order { get; }", "new(\"R\", 1)", "new(\"R\", 2)")]
        [InlineData("public int Code { get; }", "new(1)", "new(1)")]
        [InlineData("public int Code { get; }", "new(Shared)", "new(code: 1)")]
        [InlineData("public string? Code { get; }", "new(null)", "new(null)")]
        public async Task DuplicateConstantKey_ReportsCV0010(string members, string first, string second)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    private const int Shared = 1;
                    {{members}}
                    public static readonly Color Red = {{first}};
                    public static readonly Color Blue = {{second}};
                }
                """);

            Diagnostic duplicate = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "CV0010");
            Assert.Contains("'Blue'", duplicate.GetMessage());
            Assert.Contains("'Red'", duplicate.GetMessage());
        }

        [Theory]
        [InlineData("new(1)", "new(2)")]
        [InlineData("new(Next())", "new(Next())")]
        public async Task DistinctOrComputedKeys_HaveNoCV0010(string first, string second)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    private static int counter;
                    public int Code { get; }
                    private static int Next() => ++counter;
                    public static readonly Color Red = {{first}};
                    public static readonly Color Blue = {{second}};
                }
                """);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CV0010");
        }

        [Theory]
        [InlineData("public void Switch() { }")]
        [InlineData("public int Map() => 0;")]
        public async Task SwitchAndMapNames_ReportCV0008(string member)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                    {{member}}
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0008");
        }

        private static T ParseVia<T>(string text)
            where T : IParsable<T> =>
            T.Parse(text, null);

        private static bool TryParseVia<T>(string text, out T? result)
            where T : IParsable<T> =>
            T.TryParse(text, null, out result);

        private Task<ImmutableArray<Diagnostic>> CompileAsync(string source) =>
            compiler.CompileAsync(source, TestContext.Current.CancellationToken);
    }
}
