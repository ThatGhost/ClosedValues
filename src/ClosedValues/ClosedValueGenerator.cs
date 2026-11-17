using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClosedValues
{
    /// <summary>
    /// Generates the private constructor, equality, <c>All</c>, <c>Match</c> and <c>ToString</c>
    /// for every sealed partial class marked with <c>[ClosedValue]</c>.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ClosedValueGenerator : IIncrementalGenerator
    {
        private readonly ClosedValueSymbols symbols = new();

        private readonly SymbolDisplayFormat typeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(output =>
            {
                output.AddEmbeddedAttributeDefinition();
                output.AddSource(ClosedValueNames.AttributeHintName, ClosedValueNames.AttributeSource);
            });

            IncrementalValuesProvider<ClosedValueModel> models = context.SyntaxProvider
                .ForAttributeWithMetadataName(ClosedValueNames.AttributeMetadataName, IsPartialClass, CreateModel)
                .Where(model => model is not null)
                .Select((model, _) => model!);

            context.RegisterSourceOutput(models, (output, model) =>
                output.AddSource(model.HintName, new ClosedValueSourceWriter(model).Write()));
        }

        private bool IsPartialClass(SyntaxNode node, CancellationToken cancellationToken) =>
            node is ClassDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        private ClosedValueModel? CreateModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.TargetSymbol is not INamedTypeSymbol type || !symbols.HasSupportedShape(type))
            {
                return null;
            }

            ImmutableArray<ClosedValueMember> members = symbols.GetStateProperties(type)
                .Select(property => new ClosedValueMember(
                    property.Name,
                    property.Type.ToDisplayString(typeFormat),
                    property.Type.SpecialType == SpecialType.System_String,
                    symbols.IsBasicType(property.Type)))
                .ToImmutableArray();

            ImmutableArray<string> instances = symbols.GetInstances(type)
                .Select(field => field.Name)
                .ToImmutableArray();

            bool supportsNullable = context.SemanticModel.Compilation is CSharpCompilation compilation
                && compilation.LanguageVersion >= LanguageVersion.CSharp8;

            return new ClosedValueModel(
                type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString(),
                type.Name,
                supportsNullable,
                context.SemanticModel.Compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.NotNullWhenAttribute") is not null,
                symbols.DeclaresToString(type),
                new EquatableArray<ClosedValueMember>(members),
                new EquatableArray<string>(instances));
        }
    }
}
