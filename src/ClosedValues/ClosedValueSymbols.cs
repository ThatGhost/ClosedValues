using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClosedValues
{
    /// <summary>
    /// Reads the shape of a closed value type from its symbol, for the generator and the analyzer alike.
    /// </summary>
    internal sealed class ClosedValueSymbols
    {
        public bool IsClosedValue(INamedTypeSymbol type) =>
            type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == ClosedValueNames.AttributeMetadataName);

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

        public bool HasTryParse(INamedTypeSymbol type)
        {
            IPropertySymbol[] properties = GetStateProperties(type).ToArray();
            return properties.Length == 1 && IsBasicType(properties[0].Type);
        }

        /// <summary>Whether the instances can be ordered by a single member of this type: a basic type other than bool or string.</summary>
        public bool IsOrderedType(ITypeSymbol type) =>
            IsBasicType(type) && type.SpecialType is not (SpecialType.System_Boolean or SpecialType.System_String);

        public bool HasOrdering(INamedTypeSymbol type)
        {
            IPropertySymbol[] properties = GetStateProperties(type).ToArray();
            return properties.Length == 1 && IsOrderedType(properties[0].Type);
        }

        public bool DeclaresToString(INamedTypeSymbol type) =>
            type.GetMembers(nameof(ToString)).OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 0 && !method.IsImplicitlyDeclared && !IsGeneratedOnly(method));

        public ISet<string> GetGeneratedMemberNames(INamedTypeSymbol type)
        {
            HashSet<string> names = new(StringComparer.Ordinal)
            {
                "All",
                "allInstances",
                "FromName",
                "TryFromName",
                "ClosedValueTypeConverter",
                "ClosedValueJsonConverter",
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
            }

            if (HasTryParse(type))
            {
                names.Add("TryParse");
                names.Add("Parse");
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
