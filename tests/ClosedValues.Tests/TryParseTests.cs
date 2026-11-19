using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ClosedValues.Tests
{
    public sealed class TryParseTests
    {
        private readonly SnippetCompiler compiler = new();

        [Fact]
        public void StringMember_FindsTheInstanceWithAnEqualValue()
        {
            Assert.True(Visibility.TryParse("public", out Visibility? visibility));
            Assert.Same(Visibility.Public, visibility);
        }

        [Fact]
        public void StringMember_ComparesOrdinalByDefault()
        {
            Assert.False(Visibility.TryParse("PUBLIC", out Visibility? visibility));
            Assert.Null(visibility);
        }

        [Fact]
        public void StringMember_UsesTheGivenComparison()
        {
            Assert.True(Visibility.TryParse("PUBLIC", StringComparison.OrdinalIgnoreCase, out Visibility? visibility));
            Assert.Same(Visibility.Public, visibility);
        }

        [Fact]
        public void StringMember_ReturnsFalseForNull()
        {
            Assert.False(Visibility.TryParse(null, out _));
        }

        [Fact]
        public void IntMember_FindsTheInstanceWithAnEqualValue()
        {
            Assert.True(Priority.TryParse(2, out Priority? priority));
            Assert.Same(Priority.Medium, priority);
            Assert.False(Priority.TryParse(4, out _));
        }

        [Theory]
        [InlineData("public string Name { get; } public int Order { get; }")]
        [InlineData("public System.Guid Id { get; }")]
        public async Task OtherShapes_HaveNoKeyTryParse(string members)
        {
            ImmutableArray<Diagnostic> diagnostics = await compiler.CompileAsync($$"""
                using ClosedValues;

                [ClosedValue]
                public sealed partial class Sample
                {
                    {{members}}
                }

                public static class Caller
                {
                    public static bool Call() => Sample.TryParse(default!, out _);
                }
                """, TestContext.Current.CancellationToken);

            // Only the IParsable overload with a format provider exists, so the key-style call does not compile.
            Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal));
        }
    }
}
