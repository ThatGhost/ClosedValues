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
    /// for every partial class, record or struct marked with <c>[ClosedValue]</c>.
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

            IncrementalValueProvider<ClosedValueSwitches> projectSwitches = context.AnalyzerConfigOptionsProvider
                .Select((options, _) => symbols.ReadProjectSwitches(options.GlobalOptions));

            IncrementalValuesProvider<ClosedValueModel> models = context.SyntaxProvider
                .ForAttributeWithMetadataName(ClosedValueNames.AttributeMetadataName, IsPartialType, CreateModel)
                .Where(model => model is not null)
                .Combine(projectSwitches)
                .Select((pair, _) => pair.Left! with { ProjectSwitches = pair.Right });

            context.RegisterSourceOutput(models, (output, model) =>
                output.AddSource(model.HintName, new ClosedValueSourceWriter(model).Write()));
        }

        private bool IsPartialType(SyntaxNode node, CancellationToken cancellationToken) =>
            symbols.IsSupportedDeclaration(node) && ((TypeDeclarationSyntax)node).Modifiers.Any(SyntaxKind.PartialKeyword);

        private ClosedValueModel? CreateModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.TargetSymbol is not INamedTypeSymbol type || !symbols.HasSupportedShape(type))
            {
                return null;
            }

            IPropertySymbol? key = symbols.GetKeyProperty(type);
            ImmutableArray<ClosedValueMember> members = symbols.GetStateProperties(type)
                .Select(property => new ClosedValueMember(
                    property.Name,
                    property.Type.ToDisplayString(typeFormat),
                    property.Type.SpecialType == SpecialType.System_String,
                    symbols.IsBasicType(property.Type),
                    symbols.IsOrderedType(property.Type),
                    property.Type.TypeKind == TypeKind.Enum ? "Enum" : property.Type.SpecialType.ToString().Replace("System_", string.Empty),
                    SymbolEqualityComparer.Default.Equals(property, key)))
                .ToImmutableArray();

            ImmutableArray<string> instances = symbols.GetInstances(type)
                .Select(field => field.Name)
                .ToImmutableArray();

            Compilation compilation = context.SemanticModel.Compilation;
            bool typeConverter = compilation.GetTypeByMetadataName("System.ComponentModel.TypeConverter") is not null;
            INamedTypeSymbol? jsonConverter = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonConverter`1");
            ClosedValueConverters converters = new(
                typeConverter,
                typeConverter && !symbols.HasAttribute(type, "System.ComponentModel.TypeConverterAttribute"),
                typeConverter && jsonConverter is not null,
                typeConverter && jsonConverter is not null && !symbols.HasAttribute(type, "System.Text.Json.Serialization.JsonConverterAttribute"),
                jsonConverter is not null && jsonConverter.GetMembers("ReadAsPropertyName").Any());

            LanguageVersion languageVersion = compilation is CSharpCompilation csharpCompilation ? csharpCompilation.LanguageVersion : LanguageVersion.Default;
            bool supportsNullable = languageVersion >= LanguageVersion.CSharp8;

            bool isParsable = symbols.IsParsable(compilation);

            return new ClosedValueModel(
                type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString(),
                type.Name,
                type.IsRecord,
                type.IsValueType,
                supportsNullable,
                compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.NotNullWhenAttribute") is not null,
                symbols.DeclaresToString(type),
                isParsable,
                converters,
                symbols.ReadTypeSwitches(type),
                ClosedValueSwitches.None,
                new EquatableArray<ClosedValueMember>(members),
                new EquatableArray<string>(instances));
        }
    }
}
