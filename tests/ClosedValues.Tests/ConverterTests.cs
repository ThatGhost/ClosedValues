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
    public sealed class ConverterTests
    {
        private readonly SnippetCompiler compiler = new();

        [Fact]
        public void Json_WritesTheSingleMember()
        {
            Assert.Equal("3", JsonSerializer.Serialize(Priority.High));
            Assert.Equal("\"public\"", JsonSerializer.Serialize(Visibility.Public));
            Assert.Equal("10", JsonSerializer.Serialize(Coin.Dime));
        }

        [Fact]
        public void Json_WritesTheFieldNameForSeveralMembers()
        {
            Assert.Equal("\"Earth\"", JsonSerializer.Serialize(Planet.Earth));
            Assert.Equal("\"Winter\"", JsonSerializer.Serialize(Season.Winter));
            Assert.Equal("\"Hearts\"", JsonSerializer.Serialize(Suit.Hearts));
        }

        [Fact]
        public void Json_ReadsTheInstance()
        {
            Assert.Same(Priority.Medium, JsonSerializer.Deserialize<Priority>("2"));
            Assert.Same(Visibility.Private, JsonSerializer.Deserialize<Visibility>("\"private\""));
            Assert.Same(Planet.Mars, JsonSerializer.Deserialize<Planet>("\"Mars\""));
            Assert.Equal(Suit.Spades, JsonSerializer.Deserialize<Suit>("\"Spades\""));
            Assert.Equal(Coin.Penny, JsonSerializer.Deserialize<Coin>("1"));
        }

        [Fact]
        public void Json_RoundTripsAnObjectWithClosedValues()
        {
            Order order = new() { Priority = Priority.Low, Planet = Planet.Venus, Suit = Suit.Clubs, Coins = [Coin.Dime, Coin.Quarter] };

            string json = JsonSerializer.Serialize(order);
            Order? read = JsonSerializer.Deserialize<Order>(json);

            Assert.Equal("{\"Priority\":1,\"Planet\":\"Venus\",\"Suit\":\"Clubs\",\"Coins\":[10,25],\"OptionalSuit\":null}", json);
            Assert.NotNull(read);
            Assert.Same(Priority.Low, read.Priority);
            Assert.Same(Planet.Venus, read.Planet);
            Assert.Equal(Suit.Clubs, read.Suit);
            Assert.Equal([Coin.Dime, Coin.Quarter], read.Coins);
            Assert.Null(read.OptionalSuit);
        }

        [Fact]
        public void Json_UsesTheTextAsADictionaryKey()
        {
            Dictionary<Planet, int> moons = new() { [Planet.Earth] = 1, [Planet.Mars] = 2 };
            Dictionary<Priority, string> labels = new() { [Priority.High] = "urgent" };

            Assert.Equal("{\"Earth\":1,\"Mars\":2}", JsonSerializer.Serialize(moons));
            Assert.Equal("{\"3\":\"urgent\"}", JsonSerializer.Serialize(labels));
            Assert.Equal(2, JsonSerializer.Deserialize<Dictionary<Planet, int>>("{\"Mars\":2}")![Planet.Mars]);
            Assert.Equal("urgent", JsonSerializer.Deserialize<Dictionary<Priority, string>>("{\"3\":\"urgent\"}")![Priority.High]);
        }

        [Fact]
        public void Json_ThrowsForAnUnknownValue()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Priority>("9"));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Planet>("\"Pluto\""));
        }

        [Fact]
        public void Json_ThrowsForAStructDefault()
        {
            Suit[] uninitialized = new Suit[1];

            Assert.Throws<JsonException>(() => JsonSerializer.Serialize(uninitialized[0]));
        }

        [Fact]
        public void TypeConverter_ConvertsToAndFromText()
        {
            TypeConverter planets = TypeDescriptor.GetConverter(typeof(Planet));
            TypeConverter priorities = TypeDescriptor.GetConverter(typeof(Priority));
            TypeConverter suits = TypeDescriptor.GetConverter(typeof(Suit));

            Assert.Same(Planet.Jupiter, planets.ConvertFromInvariantString("Jupiter"));
            Assert.Equal("Jupiter", planets.ConvertToInvariantString(Planet.Jupiter));
            Assert.Same(Priority.High, priorities.ConvertFromInvariantString("3"));
            Assert.Equal("3", priorities.ConvertToInvariantString(Priority.High));
            Assert.Equal(Suit.Diamonds, suits.ConvertFromInvariantString("Diamonds"));
            Assert.True(planets.CanConvertFrom(typeof(string)));
            Assert.True(planets.CanConvertTo(typeof(string)));
        }

        [Fact]
        public void TypeConverter_ThrowsForUnknownText()
        {
            Assert.Throws<FormatException>(() => TypeDescriptor.GetConverter(typeof(Priority)).ConvertFromInvariantString("nine"));
            Assert.Throws<FormatException>(() => TypeDescriptor.GetConverter(typeof(Planet)).ConvertFromInvariantString("Pluto"));
        }

        [Theory]
        [InlineData("char", "'a'")]
        [InlineData("bool", "true")]
        [InlineData("byte", "(byte)7")]
        [InlineData("long", "7L")]
        [InlineData("double", "1.5")]
        [InlineData("decimal", "1.5m")]
        [InlineData("System.DateTime", "new System.DateTime(2026, 1, 2)")]
        [InlineData("System.DayOfWeek", "System.DayOfWeek.Friday")]
        public async Task Converters_CompileForEveryBasicMemberType(string memberType, string memberValue)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Sample
                {
                    public {{memberType}} Value { get; }
                    public static readonly Sample First = new({{memberValue}});
                    public static string Json() => System.Text.Json.JsonSerializer.Serialize(First);
                    public static bool Text() => Sample.ClosedValueTypeConverter.FromText(Sample.ClosedValueTypeConverter.ToText(First), out _);
                }
                """);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Fact]
        public async Task OwnConverterAttributes_AreKept()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                [System.ComponentModel.TypeConverter(typeof(System.ComponentModel.StringConverter))]
                [System.Text.Json.Serialization.JsonConverter(typeof(MyConverter))]
                public sealed partial class Sample
                {
                    public string Name { get; }
                    public static readonly Sample First = new("first");
                }

                public sealed class MyConverter : System.Text.Json.Serialization.JsonConverter<Sample>
                {
                    public override Sample? Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => Sample.First;
                    public override void Write(System.Text.Json.Utf8JsonWriter writer, Sample value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNullValue();
                }
                """);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        private Task<ImmutableArray<Diagnostic>> CompileAsync(string source) =>
            compiler.CompileAsync(source, TestContext.Current.CancellationToken);

        public sealed class Order
        {
            public Priority? Priority { get; set; }

            public Planet? Planet { get; set; }

            public Suit Suit { get; set; } = Suit.Hearts;

            public List<Coin> Coins { get; set; } = [];

            public Suit? OptionalSuit { get; set; }
        }
    }
}
