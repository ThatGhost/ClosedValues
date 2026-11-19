using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClosedValues
{
    /// <summary>
    /// Reads the shape of a closed value type from its symbol, for the generator and the analyzer alike.
    /// </summary>
    internal sealed class ClosedValueSymbols
    {
        public bool IsClosedValue(INamedTypeSymbol type) => GetClosedValueAttribute(type) is not null;

        public AttributeData? GetClosedValueAttribute(INamedTypeSymbol type) =>
            type.GetAttributes().FirstOrDefault(attribute => IsAttribute(attribute, ClosedValueNames.AttributeMetadataName));

        /// <summary>
        /// Whether the attribute has the metadata name. Another generator cannot see the attribute source that this
        /// generator adds, so there the attribute class is an error type, and only its name can be compared.
        /// </summary>
        public bool IsAttribute(AttributeData attribute, string metadataName)
        {
            if (attribute.AttributeClass is not INamedTypeSymbol attributeClass)
            {
                return false;
            }

            if (attributeClass.TypeKind != TypeKind.Error)
            {
                return attributeClass.ToDisplayString() == metadataName;
            }

            string name = metadataName.Substring(metadataName.LastIndexOf('.') + 1);
            return attributeClass.Name == name || attributeClass.Name + "Attribute" == name;
        }

        public bool HasSupportedShape(INamedTypeSymbol type) =>
            (type.TypeKind == TypeKind.Class || (type.TypeKind == TypeKind.Struct && !type.IsRefLikeType))
            && type.ContainingType is null
            && !type.IsGenericType
            && !type.IsStatic;

        public bool IsSupportedDeclaration(SyntaxNode node) =>
            node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax;

        public bool HasAttribute(INamedTypeSymbol type, string metadataName) =>
            type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

        public bool IsAutoProperty(IPropertySymbol property) =>
            property.ContainingType.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));

        public IEnumerable<IPropertySymbol> GetStateProperties(INamedTypeSymbol type) =>
            type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => !property.IsStatic && !property.IsIndexer && property.SetMethod is null && IsAutoProperty(property));

        public IEnumerable<IFieldSymbol> GetInstances(INamedTypeSymbol type) =>
            type.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(field => field.IsStatic && field.IsReadOnly && !field.IsImplicitlyDeclared && SymbolEqualityComparer.Default.Equals(field.Type, type));

        public bool IsMarkedKey(IPropertySymbol property) =>
            property.GetAttributes().Any(attribute => IsAttribute(attribute, ClosedValueNames.KeyAttributeMetadataName));

        /// <summary>
        /// Returns the key: the one state property marked with [ClosedValueKey], or without a marker the single state
        /// property. The key must have a basic type; otherwise the type has no key.
        /// </summary>
        public IPropertySymbol? GetKeyProperty(INamedTypeSymbol type)
        {
            IPropertySymbol[] properties = GetStateProperties(type).ToArray();
            IPropertySymbol[] marked = properties.Where(IsMarkedKey).ToArray();
            IPropertySymbol? key = marked.Length switch
            {
                0 => properties.Length == 1 ? properties[0] : null,
                1 => marked[0],
                _ => null,
            };

            return key is not null && IsBasicType(key.Type) ? key : null;
        }

        public bool HasTryParse(INamedTypeSymbol type) => GetKeyProperty(type) is not null;

        /// <summary>
        /// Whether the generator implements IParsable&lt;T&gt;: it needs static abstract interface members (C# 11), the
        /// interface and its nullable attributes, and the type converter that reads the text.
        /// </summary>
        /// <summary>Reads the switches that are set on the [ClosedValue] attribute of the type.</summary>
        public ClosedValueSwitches ReadTypeSwitches(INamedTypeSymbol type)
        {
            AttributeData? attribute = GetClosedValueAttribute(type);
            AttributeArgumentListSyntax? arguments = (attribute?.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax)?.ArgumentList;
            bool? Read(string name)
            {
                foreach (KeyValuePair<string, TypedConstant> argument in attribute?.NamedArguments ?? [])
                {
                    if (argument.Key == name && argument.Value.Value is bool value)
                    {
                        return value;
                    }
                }

                // An unresolved attribute has no named arguments, so read a literal true or false from the syntax.
                foreach (AttributeArgumentSyntax argument in arguments?.Arguments ?? default)
                {
                    if (argument.NameEquals?.Name.Identifier.ValueText == name)
                    {
                        return argument.Expression.Kind() switch
                        {
                            SyntaxKind.TrueLiteralExpression => true,
                            SyntaxKind.FalseLiteralExpression => false,
                            _ => null,
                        };
                    }
                }

                return null;
            }

            return new ClosedValueSwitches(Read("Json"), Read("TypeConverter"), Read("Parsable"), Read("EfCore"));
        }

        /// <summary>Reads the MSBuild properties ClosedValuesJson, ClosedValuesTypeConverter, ClosedValuesParsable and ClosedValuesEfCore.</summary>
        public ClosedValueSwitches ReadProjectSwitches(AnalyzerConfigOptions options)
        {
            bool? Read(string name) =>
                options.TryGetValue("build_property.ClosedValues" + name, out string? text) && bool.TryParse(text?.Trim(), out bool value)
                    ? value
                    : null;

            return new ClosedValueSwitches(Read("Json"), Read("TypeConverter"), Read("Parsable"), Read("EfCore"));
        }

        public bool IsParsable(Compilation compilation) =>
            compilation is CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp11 }
            && compilation.GetTypeByMetadataName("System.ComponentModel.TypeConverter") is not null
            && compilation.GetTypeByMetadataName("System.IParsable`1") is not null
            && compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute") is not null;

        /// <summary>Whether the instances can be ordered by a single member of this type: a basic type other than bool or string.</summary>
        public bool IsOrderedType(ITypeSymbol type) =>
            IsBasicType(type) && type.SpecialType is not (SpecialType.System_Boolean or SpecialType.System_String);

        public bool HasOrdering(INamedTypeSymbol type) => GetKeyProperty(type) is IPropertySymbol key && IsOrderedType(key.Type);

        public bool DeclaresToString(INamedTypeSymbol type) =>
            type.GetMembers(nameof(ToString)).OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 0 && !method.IsImplicitlyDeclared && !IsGeneratedOnly(method));

        public ISet<string> GetGeneratedMemberNames(INamedTypeSymbol type, Compilation compilation, ClosedValueSwitches projectSwitches)
        {
            bool isParsable = IsParsable(compilation) && ReadTypeSwitches(type).Over(projectSwitches).Parsable != false;
            HashSet<string> names = new(StringComparer.Ordinal)
            {
                "All",
                "allInstances",
                "FromName",
                "TryFromName",
                "ClosedValueTypeConverter",
                "ClosedValueJsonConverter",
                "ClosedValueEfCoreConverter",
            };

            if (!type.IsRecord)
            {
                names.Add(nameof(Equals));
                names.Add(nameof(GetHashCode));
                names.Add("op_Equality");
                names.Add("op_Inequality");
            }

            if (GetInstances(type).Any())
            {
                names.Add("Match");
                names.Add("Switch");
                names.Add("Map");
            }

            if (HasTryParse(type) || isParsable)
            {
                names.Add("TryParse");
                names.Add("Parse");
            }

            if (HasTryParse(type))
            {
                names.Add("op_Implicit");
                names.Add("op_Explicit");
            }

            if (HasOrdering(type))
            {
                names.Add("CompareTo");
                names.Add("op_LessThan");
                names.Add("op_LessThanOrEqual");
                names.Add("op_GreaterThan");
                names.Add("op_GreaterThanOrEqual");
            }

            return names;
        }

        public bool IsGeneratedOnly(ISymbol symbol) =>
            symbol.DeclaringSyntaxReferences.Length > 0
            && symbol.DeclaringSyntaxReferences.All(reference => IsGenerated(reference.SyntaxTree));

        public bool IsBasicType(ITypeSymbol type) =>
            type.TypeKind == TypeKind.Enum || type.SpecialType switch
            {
                SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_String
                    or SpecialType.System_SByte or SpecialType.System_Byte
                    or SpecialType.System_Int16 or SpecialType.System_UInt16
                    or SpecialType.System_Int32 or SpecialType.System_UInt32
                    or SpecialType.System_Int64 or SpecialType.System_UInt64
                    or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal
                    or SpecialType.System_DateTime => true,
                _ => false,
            };

        public bool IsGenerated(SyntaxTree tree) =>
            tree.FilePath.EndsWith(ClosedValueNames.GeneratedFileSuffix, StringComparison.Ordinal);
    }
}
