using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ClosedValues.Tests
{
    public sealed class LookupMatchOrderingTests
    {
        private readonly SnippetCompiler compiler = new();

        [Fact]
        public void FromName_ReturnsTheInstanceWithTheFieldName()
        {
            Assert.Same(Planet.Earth, Planet.FromName("Earth"));
            Assert.Same(Season.Winter, Season.FromName("Winter"));
            Assert.Equal(Suit.Hearts, Suit.FromName("Hearts"));
            Assert.Equal(Coin.Dime, Coin.FromName("Dime"));
        }

        [Fact]
        public void FromName_FindsAnInstanceWhoseNameIsAKeyword()
        {
            Assert.Same(Visibility.Default, Visibility.FromName("Default"));
        }

        [Fact]
        public void TryFromName_ComparesOrdinalByDefault()
        {
            Assert.False(Planet.TryFromName("earth", out Planet? planet));
            Assert.Null(planet);
        }

        [Fact]
        public void TryFromName_UsesTheGivenComparison()
        {
            Assert.True(Planet.TryFromName("earth", StringComparison.OrdinalIgnoreCase, out Planet? planet));
            Assert.Same(Planet.Earth, planet);
        }

        [Fact]
        public void TryFromName_ReturnsFalseForNullAndUnknownNames()
        {
            Assert.False(Planet.TryFromName(null, out _));
            Assert.False(Suit.TryFromName("Stars", out Suit suit));
            Assert.Null(suit.Name);
        }

        [Fact]
        public void FromName_ThrowsForAnUnknownName()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => Planet.FromName("Pluto"));
            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void Parse_ReturnsTheInstanceWithTheValue()
        {
            Assert.Same(Priority.Medium, Priority.Parse(2));
            Assert.Equal(Coin.Nickel, Coin.Parse(5));
            Assert.Same(Visibility.Public, Visibility.Parse("PUBLIC", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Parse_ThrowsForAnUnknownValue()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => Priority.Parse(9));
            Assert.Equal("value", exception.ParamName);
            Assert.Throws<ArgumentException>(() => Visibility.Parse("PUBLIC"));
        }

        [Fact]
        public void MatchWithFallback_CallsTheFunctionOfTheInstance()
        {
            Assert.Equal(1, Planet.Earth.Match(@default: () => 0, earth: () => 1));
        }

        [Fact]
        public void MatchWithFallback_CallsTheFallbackForOtherInstances()
        {
            Assert.Equal(0, Planet.Mars.Match(@default: () => 0, earth: () => 1));
            Assert.Equal("other", Season.Spring.Match(@default: () => "other", winter: () => "cold"));
            Assert.Equal(25, Coin.Quarter.Match(@default: () => 25, penny: () => 1));
        }

        [Fact]
        public void MatchWithFallback_CallsTheFallbackForAStructDefault()
        {
            Suit[] uninitialized = new Suit[1];

            Assert.Equal("none", uninitialized[0].Match(@default: () => "none", hearts: () => "hearts"));
        }

        [Fact]
        public void Ordering_ComparesTheSingleMember()
        {
            Assert.True(Priority.Low < Priority.High);
            Assert.True(Priority.High > Priority.Medium);
            Assert.True(Priority.Medium <= Priority.All[1]);
            Assert.True(Priority.Medium >= Priority.Low);
            Assert.False(Priority.High < Priority.Low);
            Assert.True(Coin.Penny < Coin.Quarter);
        }

        [Fact]
        public void Ordering_SortsTheInstances()
        {
            Assert.Equal([Priority.Low, Priority.Medium, Priority.High], new[] { Priority.High, Priority.Low, Priority.Medium }.Order());
            Assert.Equal([Coin.Penny, Coin.Dime], new[] { Coin.Dime, Coin.Penny }.Order());
        }

        [Fact]
        public void Ordering_PutsNullFirst()
        {
            Assert.Equal(1, Priority.Low.CompareTo(null));
            Assert.True(null < Priority.Low);
            Assert.False(Priority.Low < null);
        }

        [Fact]
        public void Conversion_ToTheValueIsImplicit()
        {
            int level = Priority.High;
            string keyword = Visibility.Private;
            int cents = Coin.Quarter;

            Assert.Equal(3, level);
            Assert.Equal("private", keyword);
            Assert.Equal(25, cents);
        }

        [Fact]
        public void Conversion_ToTheValueThrowsForNull()
        {
            Priority? priority = null;

            Assert.Throws<ArgumentNullException>(() => (int)priority!);
        }

        [Fact]
        public void Conversion_FromTheValueIsExplicitAndParses()
        {
            Assert.Same(Priority.Medium, (Priority)2);
            Assert.Equal(Coin.Dime, (Coin)10);
            Assert.Same(Visibility.Public, (Visibility)"public");
            Assert.Throws<ArgumentException>(() => (Priority)9);
        }

        [Theory]
        [InlineData("public string Name { get; }", "new(\"Red\")")]
        [InlineData("public bool IsWarm { get; }", "new(true)")]
        [InlineData("public string Name { get; } public int Code { get; }", "new(\"Red\", 1)")]
        public async Task Ordering_IsOnlyGeneratedForASingleOrderedMember(string members, string creation)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    {{members}}
                    public static readonly Color Red = {{creation}};
                    public static bool Less() => Red < Red;
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CS0019");
        }

        [Fact]
        public async Task Conversions_AreNotGeneratedForSeveralMembers()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    public string Name { get; }
                    public int Code { get; }
                    public static readonly Color Red = new("Red", 1);
                    public static string Text() => Red;
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CS0029");
        }

        [Fact]
        public void MatchWithFallback_IsADiscardWhenAnInstanceIsNamedDefault()
        {
            Assert.Equal("other", Visibility.Public.Match(_: () => "other", @private: () => "private"));
            Assert.Equal("default", Visibility.Default.Match(_: () => "other", @default: () => "default"));
        }

        [Fact]
        public async Task InstancesNamedDefaultAndDiscard_GetALongerFallbackName()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    public string Name { get; }
                    public static readonly Color Default = new("Default");
                    public static readonly Color _ = new("Unknown");
                    public static readonly Color Red = new("Red");
                    public int Code() => Red.Match(__: () => 0, red: () => 1);
                }
                """);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Theory]
        [InlineData("public static object FromName() => null!;")]
        [InlineData("public static object TryFromName() => null!;")]
        [InlineData("public static object Parse(string value) => null!;")]
        [InlineData("public int CompareTo(object other) => 0;")]
        public async Task GeneratedMemberName_ReportsCV0008(string member)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Level
                {
                    public int Value { get; }
                    public static readonly Level Low = new(1);
                    {{member}}
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0008");
        }

        [Fact]
        public async Task ParseOnATypeWithSeveralMembers_HasNoCV0008()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    public string Name { get; }
                    public int Code { get; }
                    public static readonly Color Red = new("Red", 1);
                    public static Color Parse(string text) => Red;
                }
                """);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CV0008");
        }

        private Task<ImmutableArray<Diagnostic>> CompileAsync(string source) =>
            compiler.CompileAsync(source, TestContext.Current.CancellationToken);
    }
}
