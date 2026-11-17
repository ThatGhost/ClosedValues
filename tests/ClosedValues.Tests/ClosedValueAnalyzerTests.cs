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

        [Theory]
        [InlineData("public sealed partial record Color")]
        [InlineData("public sealed partial record class Color")]
        [InlineData("public partial struct Color")]
        [InlineData("public readonly partial struct Color")]
        [InlineData("public partial record struct Color")]
        [InlineData("public readonly partial record struct Color")]
        public async Task ValidRecordOrStruct_HasNoDiagnostics(string declaration)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                namespace Sample
                {
                    [ClosedValue]
                    {{declaration}}
                    {
                        public string Name { get; }
                        public int Code { get; }
                        public bool IsWarm => this == Red;
                        public static readonly Color Red = new("Red", 1);
                        public static readonly Color Blue = new("Blue", 2);
                    }

                    public static class Usage
                    {
                        public static int Code(Color color) => color.Match(red: () => 1, blue: () => 2);
                        public static int Count() => Color.All.Count;
                    }
                }
                """);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Fact]
        public async Task StructWithoutMembers_HasNoDiagnostics()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public partial struct Unit
                {
                    public static readonly Unit Value = new();
                }
                """);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }

        [Theory]
        [InlineData("Color color = default;")]
        [InlineData("Color color = default(Color);")]
        [InlineData("var color = default(Color);")]
        public async Task StructDefaultOutsideTheType_ReportsCV0006(string statement)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public partial struct Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                }

                public static class Usage
                {
                    public static object Make()
                    {
                        {{statement}}
                        return color;
                    }
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0006");
        }

        [Fact]
        public async Task StructDefaultAsAParameterDefault_ReportsCV0006()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public partial struct Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                }

                public static class Usage
                {
                    public static string Describe(Color color = default) => color.Name;
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0006");
        }

        [Fact]
        public async Task StructDefaultInsideTheType_HasNoCV0006()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public partial struct Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                    public bool IsUnset => this == default;
                }
                """);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CV0006");
        }

        [Fact]
        public async Task ClassDefault_HasNoCV0006()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                }

                public static class Usage
                {
                    public static Color? Make() => default;
                }
                """);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CV0006");
        }

        [Fact]
        public async Task StructParameterlessNewOutsideTheType_IsReported()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public partial struct Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                }

                public static class Usage
                {
                    public static object Make() => new Color();
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id is "CV0001" or "CV0006");
        }

        [Fact]
        public async Task PositionalRecord_ReportsCV0002AndCV0005()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial record Color(string Name)
                {
                    public static readonly Color Red = new("Red");
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0002");
            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0005");
        }

        [Fact]
        public async Task UnsealedRecord_ReportsCV0004()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public partial record Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0004");
        }

        [Theory]
        [InlineData("public struct Color")]
        [InlineData("public sealed record Color")]
        [InlineData("public record struct Color")]
        public async Task RecordOrStructWithoutPartial_ReportsCV0003(string declaration)
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                {{declaration}}
                {
                    public string Name { get; }
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0003");
        }

        [Fact]
        public async Task RefStruct_ReportsCV0007()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public ref partial struct Color
                {
                    public string Name { get; }
                }
                """);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CV0007");
        }

        [Fact]
        public async Task RecordWithItsOwnEquals_HasNoCV0008()
        {
            ImmutableArray<Diagnostic> diagnostics = await CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial record Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                    public bool Equals(Color? other) => other is not null && string.Equals(Name, other.Name, System.StringComparison.OrdinalIgnoreCase);
                    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
                }
                """);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CV0008");
            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        private Task<ImmutableArray<Diagnostic>> CompileAsync(string source) =>
            compiler.CompileAsync(source, TestContext.Current.CancellationToken);
    }
}
