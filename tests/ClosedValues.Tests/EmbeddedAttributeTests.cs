using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ClosedValues.Tests
{
    public sealed class EmbeddedAttributeTests
    {
        private readonly SnippetCompiler compiler = new();

        [Fact]
        public async Task ProjectWithInternalsVisibleTo_SeesOneAttribute()
        {
            MetadataReference library = compiler.CompileToReference("Library", """
                using ClosedValues;

                [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Snippet")]

                [ClosedValue]
                public sealed partial class Color
                {
                    public string Name { get; }
                    public static readonly Color Red = new("Red");
                }
                """, TestContext.Current.CancellationToken);

            ImmutableArray<Diagnostic> diagnostics = await compiler.CompileAsync("""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Shape
                {
                    public string Name { get; }
                    public static readonly Shape Circle = new("Circle");
                    public static Color Fill => Color.Red;
                }
                """, TestContext.Current.CancellationToken, library);

            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }
    }
}
