using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ClosedValues.Tests
{
    public sealed class ClosedValueAnalyzerTests
    {
        private readonly SnippetCompiler compiler = new();

        [Fact]
        public async Task ValidClosedValue_HasNoDiagnostics()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                namespace Sample
                {
                    [ClosedValue]
                    public sealed partial class Color
                    {
                        public string Name { get; }
                        public int Code { get; }
                        public bool IsWarm => this == Red;
                        public static readonly Color Red = new("Red", 1);
                        public static readonly Color Blue = new("Blue", 2);
                    }
                }
                """);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Fact]
        public async Task CreationOutsideTheTypeThroughAnOwnConstructor_ReportsCV0001()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    public Color(int code) { Name = code.ToString(); }
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                }

                public sealed class ColorFactory
                {
                    public object Create() => new Color(7);
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0001");
        }

        [Fact]
        public async Task OwnConstructor_ReportsCV0002()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    private Color() { Name = "Red"; }
                    public string Name { get; }
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0002");
        }

        [Fact]
        public async Task MissingPartial_ReportsCV0003()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed class Color
                {
                    public string Name { get; } = "Red";
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0003");
        }

        [Fact]
        public async Task MissingSealed_ReportsCV0004()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public partial class Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0004");
        }

        [Theory]
        [InlineData("public string Name { get; set; }")]
        [InlineData("public string Name { get; init; }")]
        [InlineData("private string name = \"Red\";")]
        public async Task MutableState_ReportsCV0005(string member)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    {{member}}
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0005");
        }

        [Theory]
        [InlineData("public static int All => 0;")]
        [InlineData("public int Match() => 0;")]
        [InlineData("public static bool TryParse(string value) => true;")]
        [InlineData("public override int GetHashCode() => 0;")]
        public async Task GeneratedMemberName_ReportsCV0008(string member)
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

        [Fact]
        public async Task OwnToString_ReplacesTheGeneratedOne()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                    public override string ToString() => "Color " + Name;
                }
                """);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Fact]
        public async Task NestedType_ReportsCV0007()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                public static class Outer
                {
                    [ClosedValue]
                    public sealed partial class Color
                    {
                        public string Name { get; } = "Red";
                    }
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0007");
        }

        private Task<ImmutableArray<Diagnostic>> CompileAsync(string source) =>
            compiler.CompileAsync(source, TestContext.Current.CancellationToken);
    }
}
