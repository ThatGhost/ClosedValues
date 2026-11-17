using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClosedValues.Tests
{
    /// <summary>
    /// Compiles a source snippet with the generator and the analyzer, and returns every diagnostic.
    /// </summary>
    internal sealed class SnippetCompiler
    {
        private readonly CSharpParseOptions parseOptions = new(LanguageVersion.Latest);

        private readonly ImmutableArray<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

        public async Task<ImmutableArray<Diagnostic>> CompileAsync(string source, CancellationToken cancellationToken, params MetadataReference[] extraReferences)
        {
            (Compilation generated, ImmutableArray<Diagnostic> generatorDiagnostics) = Generate("Snippet", source, extraReferences, cancellationToken);

            ImmutableArray<Diagnostic> diagnostics = await generated
                .WithAnalyzers([new ClosedValueAnalyzer()])
                .GetAllDiagnosticsAsync(cancellationToken);

            return generatorDiagnostics.AddRange(diagnostics);
        }

        public MetadataReference CompileToReference(string assemblyName, string source, CancellationToken cancellationToken)
        {
            (Compilation generated, _) = Generate(assemblyName, source, [], cancellationToken);
            using MemoryStream image = new();
            Microsoft.CodeAnalysis.Emit.EmitResult result = generated.Emit(image, cancellationToken: cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
            }

            return MetadataReference.CreateFromImage(image.ToArray());
        }

        private (Compilation Generated, ImmutableArray<Diagnostic> Diagnostics) Generate(
            string assemblyName,
            string source,
            MetadataReference[] extraReferences,
            CancellationToken cancellationToken)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source, parseOptions, cancellationToken: cancellationToken)],
                references.AddRange(extraReferences),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            GeneratorDriver driver = CSharpGeneratorDriver.Create([new ClosedValueGenerator().AsSourceGenerator()], parseOptions: parseOptions);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation generated, out ImmutableArray<Diagnostic> diagnostics, cancellationToken);
            return (generated, diagnostics);
        }
    }
}
