using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClosedValues.Tests
{
    public sealed class EfCoreAndSwitchTests
    {
        private readonly SnippetCompiler compiler = new();

        [Fact]
        public void EfCore_RoundTripsEveryKind()
        {
            using SqliteConnection connection = OpenDatabase();
            using (OrdersContext context = new(connection))
            {
                context.Orders.Add(new Order { Id = 1, Currency = Currency.Yen, Planet = Planet.Mars, Priority = Priority.High, Suit = Suit.Hearts, Coin = Coin.Dime });
                context.Orders.Add(new Order { Id = 2, Currency = Currency.Euro, Planet = Planet.Earth, Priority = Priority.Low, Suit = Suit.Spades, Coin = Coin.Penny, OptionalSuit = Suit.Clubs, OptionalPlanet = Planet.Venus });
                context.SaveChanges();
            }

            using (OrdersContext context = new(connection))
            {
                Order first = context.Orders.Single(order => order.Id == 1);
                Order second = context.Orders.Single(order => order.Id == 2);

                Assert.Same(Currency.Yen, first.Currency);
                Assert.Same(Planet.Mars, first.Planet);
                Assert.Same(Priority.High, first.Priority);
                Assert.Equal(Suit.Hearts, first.Suit);
                Assert.Equal(Coin.Dime, first.Coin);
                Assert.Null(first.OptionalSuit);
                Assert.Null(first.OptionalPlanet);
                Assert.Equal(Suit.Clubs, second.OptionalSuit);
                Assert.Same(Planet.Venus, second.OptionalPlanet);
            }
        }

        [Fact]
        public void EfCore_StoresTheKeyOrTheFieldName()
        {
            using SqliteConnection connection = OpenDatabase();
            using (OrdersContext context = new(connection))
            {
                context.Orders.Add(new Order { Id = 1, Currency = Currency.Dinar, Planet = Planet.Jupiter, Priority = Priority.Medium, Suit = Suit.Diamonds, Coin = Coin.Quarter });
                context.SaveChanges();
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Currency, Planet, Priority, Suit, Coin, typeof(Priority), typeof(Coin) FROM Orders";
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("KWD", reader.GetString(0));
            Assert.Equal("Jupiter", reader.GetString(1));
            Assert.Equal(2, reader.GetInt32(2));
            Assert.Equal("Diamonds", reader.GetString(3));
            Assert.Equal(25, reader.GetInt32(4));
            Assert.Equal("integer", reader.GetString(5));
            Assert.Equal("integer", reader.GetString(6));
        }

        [Fact]
        public void EfCore_TranslatesComparisonsInQueries()
        {
            using SqliteConnection connection = OpenDatabase();
            using (OrdersContext context = new(connection))
            {
                context.Orders.Add(new Order { Id = 1, Currency = Currency.Yen, Planet = Planet.Mars, Priority = Priority.High, Suit = Suit.Hearts, Coin = Coin.Dime });
                context.Orders.Add(new Order { Id = 2, Currency = Currency.Euro, Planet = Planet.Earth, Priority = Priority.Low, Suit = Suit.Hearts, Coin = Coin.Penny });
                context.SaveChanges();
            }

            using (OrdersContext context = new(connection))
            {
                Currency yen = Currency.Yen;

                Assert.Equal(1, context.Orders.Single(order => order.Currency == yen).Id);
                Assert.Equal(2, context.Orders.Single(order => order.Planet == Planet.Earth).Id);
                Assert.Equal(2, context.Orders.Count(order => order.Suit == Suit.Hearts));
            }
        }

        [Fact]
        public void EfCore_ConverterCanBeUsedOnOneProperty()
        {
            Currency.ClosedValueEfCoreConverter converter = new();

            Assert.Equal("JPY", converter.ConvertToProvider(Currency.Yen));
            Assert.Same(Currency.Euro, converter.ConvertFromProvider("EUR"));
        }

        [Fact]
        public async Task JsonSwitchOnTheType_RemovesTheConverter()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("[ClosedValue(Json = false)]", "Sample.ClosedValueJsonConverter", []);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CS0426");
        }

        [Fact]
        public async Task JsonSwitchInTheProject_RemovesTheConverter()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("[ClosedValue]", "Sample.ClosedValueJsonConverter", new() { ["ClosedValuesJson"] = "false" });

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CS0426");
        }

        [Fact]
        public async Task SwitchOnTheType_WinsOverTheProject()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("[ClosedValue(Json = true)]", "Sample.ClosedValueJsonConverter", new() { ["ClosedValuesJson"] = "False" });

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Theory]
        [InlineData("[ClosedValue(EfCore = false)]", "Sample.ClosedValueEfCoreConverter")]
        [InlineData("[ClosedValue(Parsable = false)]", "System.IParsable<Sample>")]
        public async Task OtherSwitches_RemoveTheirMembers(string attribute, string typeName)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync(attribute, typeName, []);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void TypeConverterSwitch_RemovesOnlyTheAttribute()
        {
            Compilation generated = compiler.Generate(Source("[ClosedValue(TypeConverter = false)]", "object"), new Dictionary<string, string>(), TestContext.Current.CancellationToken);
            INamedTypeSymbol sample = generated.GetTypeByMetadataName("Sample")!;

            Assert.DoesNotContain(sample.GetAttributes(), attribute => attribute.AttributeClass?.Name == "TypeConverterAttribute");
            Assert.Contains(sample.GetAttributes(), attribute => attribute.AttributeClass?.Name == "JsonConverterAttribute");
            Assert.NotNull(generated.GetTypeByMetadataName("Sample+ClosedValueTypeConverter"));
        }

        [Fact]
        public void EfCoreSwitch_LeavesTheTypeOutOfTheRegistration()
        {
            Compilation generated = compiler.Generate(
                """
                using ClosedValues;

                [ClosedValue(EfCore = false)]
                public sealed partial class Hidden
                {
                    public string Name { get; }
                    public static readonly Hidden First = new("first");
                }

                [ClosedValue]
                public sealed partial class Shown
                {
                    public string Name { get; }
                    public static readonly Shown First = new("first");
                }
                """,
                new Dictionary<string, string>(),
                TestContext.Current.CancellationToken);

            string registration = generated.SyntaxTrees.Single(tree => tree.FilePath.Contains("ClosedValuesModelConfiguration", StringComparison.Ordinal)).ToString();
            Assert.Contains("global::Shown", registration);
            Assert.DoesNotContain("global::Hidden", registration);
        }

        [Fact]
        public async Task ParsableSwitchedOff_FreesTheParseName()
        {
            const string source = """
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    public string Name { get; }
                    public int Code { get; }
                    public static readonly Color Red = new("Red", 1);
                    public static Color Parse(string text) => Red;
                }
                """;

            ImmutableArray<Diagnostic> withParsable = await compiler.CompileAsync(source, TestContext.Current.CancellationToken);
            ImmutableArray<Diagnostic> withoutParsable = await compiler.CompileAsync(source, new Dictionary<string, string> { ["ClosedValuesParsable"] = "false" }, TestContext.Current.CancellationToken);

            Assert.Contains(withParsable, diagnostic => diagnostic.Id == "CV0008");
            Assert.DoesNotContain(withoutParsable, diagnostic => diagnostic.Id == "CV0008");
            Assert.Empty(withoutParsable.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Fact]
        public async Task UnreadableProjectValue_IsIgnored()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("[ClosedValue]", "Sample.ClosedValueJsonConverter", new() { ["ClosedValuesJson"] = "nope" });

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        private static SqliteConnection OpenDatabase()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using OrdersContext context = new(connection);
            context.Database.EnsureCreated();
            return connection;
        }

        private Task<ImmutableArray<Diagnostic>> CompileAsync(string attribute, string typeName, Dictionary<string, string> buildProperties) =>
            compiler.CompileAsync(Source(attribute, typeName), buildProperties, TestContext.Current.CancellationToken);

        private static string Source(string attribute, string typeName) => $$"""
            using ClosedValues;

            {{attribute}}
            public sealed partial class Sample
            {
                public string Name { get; }
                public static readonly Sample First = new("first");
                public static System.Type Used() => typeof({{typeName}});
            }
            """;

        public sealed class Order
        {
            public int Id { get; set; }

            public Currency Currency { get; set; } = Currency.Euro;

            public Planet Planet { get; set; } = Planet.Earth;

            public Priority Priority { get; set; } = Priority.Low;

            public Suit Suit { get; set; } = Suit.Clubs;

            public Coin Coin { get; set; } = Coin.Penny;

            public Suit? OptionalSuit { get; set; }

            public Planet? OptionalPlanet { get; set; }
        }

        private sealed class OrdersContext(SqliteConnection connection) : DbContext
        {
            public DbSet<Order> Orders => Set<Order>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite(connection);

            protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) => configurationBuilder.ConfigureClosedValues();
        }
    }
}
