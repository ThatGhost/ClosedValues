using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClosedValues
{
    /// <summary>
    /// Generates an EF Core value converter for every closed value type, and one method per assembly that registers
    /// them all.
    /// </summary>
    /// <remarks>
    /// A generator cannot see the output of another generator. This one runs beside the ClosedValues generator, so it
    /// cannot resolve <c>[ClosedValue]</c> or <c>[ClosedValueKey]</c>, and it finds them by name. The converters it
    /// writes call Parse, FromName and the type converter of the ClosedValues generator; both outputs meet in the
    /// final compilation.
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class ClosedValueEfCoreGenerator : IIncrementalGenerator
    {
        private const string ValueConverter = "global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter";

        private readonly ClosedValueSymbols symbols = new();

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<ClosedValueSwitches> projectSwitches = context.AnalyzerConfigOptionsProvider
                .Select((options, _) => symbols.ReadProjectSwitches(options.GlobalOptions));

            IncrementalValuesProvider<EfCoreModel> models = context.SyntaxProvider
                .CreateSyntaxProvider(HasClosedValueAttribute, CreateModel)
                .Where(model => model is not null)
                .Combine(projectSwitches)
                .Select((pair, _) => pair.Left! with { ProjectSwitches = pair.Right })
                .Where(model => model.TypeSwitches.Over(model.ProjectSwitches).EfCore != false);

            // A partial type can carry the attribute on one declaration only, but collect by hint name to be safe.
            context.RegisterSourceOutput(models.Collect(), (output, all) =>
            {
                EfCoreModel[] distinct = all.GroupBy(model => model.HintName).Select(group => group.First()).ToArray();
                foreach (EfCoreModel model in distinct)
                {
                    output.AddSource(model.HintName, WriteConverter(model));
                }

                EfCoreModel[] registered = distinct.Where(model => model.HasConventions).OrderBy(model => model.HintName, StringComparer.Ordinal).ToArray();
                if (registered.Length > 0)
                {
                    output.AddSource("ClosedValuesModelConfiguration" + ClosedValueNames.GeneratedFileSuffix, WriteModelConfiguration(registered));
                }
            });
        }

        private bool HasClosedValueAttribute(SyntaxNode node, CancellationToken cancellationToken) =>
            symbols.IsSupportedDeclaration(node)
            && node is TypeDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            && declaration.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => GetName(attribute.Name) is "ClosedValue" or "ClosedValueAttribute");

        private static string GetName(NameSyntax name) => name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => string.Empty,
        };

        private EfCoreModel? CreateModel(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            Compilation compilation = context.SemanticModel.Compilation;
            if (context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) is not INamedTypeSymbol type
                || !symbols.IsClosedValue(type)
                || !symbols.HasSupportedShape(type)
                || compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter`2") is null)
            {
                return null;
            }

            IPropertySymbol? key = symbols.GetKeyProperty(type);
            bool supportsNullable = compilation is CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp8 };

            return new EfCoreModel(
                type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString(),
                type.Name,
                (type.IsRecord, type.IsValueType) switch
                {
                    (true, true) => "record struct",
                    (true, false) => "record",
                    (false, true) => "struct",
                    (false, false) => "class",
                },
                key?.Name,
                key?.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                supportsNullable,
                compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelConfigurationBuilder") is not null,
                symbols.ReadTypeSwitches(type),
                ClosedValueSwitches.None);
        }

        private static string WriteConverter(EfCoreModel model)
        {
            string type = Identifier(model.TypeName);
            string provider;
            string toProvider;
            string fromProvider;
            string form;
            if (model.KeyName is string keyName && model.KeyType is string keyType)
            {
                // The key is stored as it is, so the column keeps the key's type.
                provider = keyType;
                toProvider = $"value => value.{Identifier(keyName)}";
                fromProvider = $"key => {type}.Parse(key)";
                form = "the " + keyName;
            }
            else
            {
                // Without a key, the field name is stored, written by the type converter of the ClosedValues generator.
                provider = "string";
                toProvider = $"value => ClosedValueTypeConverter.ToText(value){(model.SupportsNullable ? "!" : string.Empty)}";
                fromProvider = $"name => {type}.FromName(name)";
                form = "the field name";
            }

            StringBuilder source = new();
            source.Append("// <auto-generated/>\n");
            if (model.SupportsNullable)
            {
                source.Append("#nullable enable\n");
            }

            source.Append('\n');
            string indent = string.Empty;
            if (model.Namespace.Length > 0)
            {
                source.Append($"namespace {model.Namespace}\n{{\n");
                indent = "    ";
            }

            source.Append($"{indent}partial {model.Keyword} {type}\n");
            source.Append($"{indent}{{\n");
            source.Append($"{indent}    /// <summary>Stores a {model.TypeName} in an EF Core column as {form} of the instance.</summary>\n");
            source.Append($"{indent}    public sealed class ClosedValueEfCoreConverter : {ValueConverter}<{type}, {provider}>\n");
            source.Append($"{indent}    {{\n");
            source.Append($"{indent}        /// <summary>Creates the converter.</summary>\n");
            source.Append($"{indent}        public ClosedValueEfCoreConverter()\n");
            source.Append($"{indent}            : base({toProvider}, {fromProvider})\n");
            source.Append($"{indent}        {{\n");
            source.Append($"{indent}        }}\n");
            source.Append($"{indent}    }}\n");
            source.Append($"{indent}}}\n");
            if (model.Namespace.Length > 0)
            {
                source.Append("}\n");
            }

            return source.ToString();
        }

        /// <summary>
        /// Writes one extension method for the assembly. The class is embedded, so other assemblies, even with
        /// InternalsVisibleTo, do not see a second copy. The ClosedValues generator defines the attribute.
        /// </summary>
        private static string WriteModelConfiguration(EfCoreModel[] models)
        {
            StringBuilder source = new();
            source.Append("// <auto-generated/>\n");
            source.Append("namespace ClosedValues\n{\n");
            source.Append("    /// <summary>Registers the EF Core converters of the closed value types in this assembly.</summary>\n");
            source.Append("    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]\n");
            source.Append("    internal static class ClosedValuesModelConfigurationBuilderExtensions\n    {\n");
            source.Append("        /// <summary>Stores every closed value type of this assembly with its generated converter. Call this in ConfigureConventions.</summary>\n");
            source.Append("        public static global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder ConfigureClosedValues(this global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder builder)\n        {\n");
            foreach (EfCoreModel model in models)
            {
                string type = "global::" + (model.Namespace.Length > 0 ? model.Namespace + "." : string.Empty) + Identifier(model.TypeName);
                source.Append($"            builder.Properties<{type}>().HaveConversion<{type}.ClosedValueEfCoreConverter>();\n");
            }

            source.Append("            return builder;\n        }\n    }\n}\n");
            return source.ToString();
        }

        private static string Identifier(string name) =>
            SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;
    }

    /// <summary>
    /// What the EF Core generator needs for one closed value type.
    /// </summary>
    internal sealed record EfCoreModel(
        string Namespace,
        string TypeName,
        string Keyword,
        string? KeyName,
        string? KeyType,
        bool SupportsNullable,
        bool HasConventions,
        ClosedValueSwitches TypeSwitches,
        ClosedValueSwitches ProjectSwitches)
    {
        public string HintName => (Namespace.Length > 0 ? Namespace + "." : string.Empty) + TypeName + ".EfCore" + ClosedValueNames.GeneratedFileSuffix;
    }
}
