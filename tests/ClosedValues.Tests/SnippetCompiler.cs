using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

        public Task<ImmutableArray<Diagnostic>> CompileAsync(string source, CancellationToken cancellationToken, params MetadataReference[] extraReferences) =>
            CompileAsync(source, new Dictionary<string, string>(), cancellationToken, extraReferences);

        /// <summary>Compiles with MSBuild properties, given by name without the build_property prefix.</summary>
        public async Task<ImmutableArray<Diagnostic>> CompileAsync(
            string source,
            IReadOnlyDictionary<string, string> buildProperties,
            CancellationToken cancellationToken,
            params MetadataReference[] extraReferences)
        {
            BuildPropertiesProvider options = new(buildProperties);
            (Compilation generated, ImmutableArray<Diagnostic> generatorDiagnostics) = Generate("Snippet", source, extraReferences, options, cancellationToken);

            ImmutableArray<Diagnostic> diagnostics = await generated
                .WithAnalyzers([new ClosedValueAnalyzer()], new AnalyzerOptions([], options))
                .GetAllDiagnosticsAsync(cancellationToken);

            return generatorDiagnostics.AddRange(diagnostics);
        }

        /// <summary>Returns the compilation with the generated sources, to inspect the generated symbols.</summary>
        public Compilation Generate(string source, IReadOnlyDictionary<string, string> buildProperties, CancellationToken cancellationToken) =>
            Generate("Snippet", source, [], new BuildPropertiesProvider(buildProperties), cancellationToken).Generated;

        public MetadataReference CompileToReference(string assemblyName, string source, CancellationToken cancellationToken)
        {
            (Compilation generated, _) = Generate(assemblyName, source, [], new BuildPropertiesProvider(new Dictionary<string, string>()), cancellationToken);
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
            AnalyzerConfigOptionsProvider options,
            CancellationToken cancellationToken)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source, parseOptions, cancellationToken: cancellationToken)],
                references.AddRange(extraReferences),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new ClosedValueGenerator().AsSourceGenerator()],
                parseOptions: parseOptions,
                optionsProvider: options);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation generated, out ImmutableArray<Diagnostic> diagnostics, cancellationToken);
            return (generated, diagnostics);
        }

        /// <summary>
        /// Gives the generator and the analyzer the MSBuild properties that the package makes visible.
        /// </summary>
        private sealed class BuildPropertiesProvider(IReadOnlyDictionary<string, string> buildProperties) : AnalyzerConfigOptionsProvider
        {
            private readonly Options global = new(buildProperties.ToDictionary(pair => "build_property." + pair.Key, pair => pair.Value));

            private readonly Options empty = new(new Dictionary<string, string>());

            public override AnalyzerConfigOptions GlobalOptions => global;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => empty;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => empty;

            private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
            {
                public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
            }
        }
    }
}
