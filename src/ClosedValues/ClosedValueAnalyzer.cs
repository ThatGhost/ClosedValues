using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ClosedValues
{
    /// <summary>
    /// Reports the code that would open a closed value type: a creation or a struct default outside the type,
    /// an own constructor, a missing <c>partial</c> or <c>sealed</c>, mutable state, and an unsupported shape.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ClosedValueAnalyzer : DiagnosticAnalyzer
    {
        private readonly ClosedValueSymbols symbols = new();

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            ClosedValueDiagnostics.CreatedOutsideType,
            ClosedValueDiagnostics.DeclaresConstructor,
            ClosedValueDiagnostics.NotPartial,
            ClosedValueDiagnostics.NotSealed,
            ClosedValueDiagnostics.MutableState,
            ClosedValueDiagnostics.ReservedMemberName,
            ClosedValueDiagnostics.DefaultValue,
            ClosedValueDiagnostics.UnsupportedShape);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
            context.RegisterOperationAction(AnalyzeDefaultValue, OperationKind.DefaultValue);
        }

        private void AnalyzeType(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol type || !symbols.IsClosedValue(type))
            {
                return;
            }

            if (!symbols.HasSupportedShape(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(ClosedValueDiagnostics.UnsupportedShape, type.Locations[0], type.Name));
                return;
            }

            ReportNotPartial(context, type);

            if (!type.IsSealed)
            {
                context.ReportDiagnostic(Diagnostic.Create(ClosedValueDiagnostics.NotSealed, type.Locations[0], type.Name));
            }

            foreach (IMethodSymbol constructor in type.InstanceConstructors.Where(constructor => !constructor.IsImplicitlyDeclared && IsUserCode(constructor)))
            {
                context.ReportDiagnostic(Diagnostic.Create(ClosedValueDiagnostics.DeclaresConstructor, constructor.Locations[0], type.Name));
            }

            ReportMutableState(context, type);
            ReportReservedMemberNames(context, type);
        }

        private void ReportReservedMemberNames(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            ISet<string> generatedNames = symbols.GetGeneratedMemberNames(type);
            foreach (ISymbol member in type.GetMembers().Where(member => !member.IsImplicitlyDeclared && generatedNames.Contains(member.Name) && IsUserCode(member)))
            {
                context.ReportDiagnostic(Diagnostic.Create(ClosedValueDiagnostics.ReservedMemberName, member.Locations[0], member.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        private void ReportNotPartial(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(context.CancellationToken) is TypeDeclarationSyntax declaration
                    && symbols.IsSupportedDeclaration(declaration)
                    && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    context.ReportDiagnostic(Diagnostic.Create(ClosedValueDiagnostics.NotPartial, declaration.Identifier.GetLocation(), type.Name));
                }
            }
        }

        private void ReportMutableState(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            foreach (ISymbol member in type.GetMembers().Where(member => !member.IsStatic && IsUserCode(member)))
            {
                bool isMutable = member switch
                {
                    IPropertySymbol property => property.SetMethod is not null && symbols.IsAutoProperty(property),
                    IFieldSymbol field => !field.IsImplicitlyDeclared && !field.IsConst,
                    _ => false,
                };

                if (isMutable)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ClosedValueDiagnostics.MutableState, member.Locations[0], member.Name));
                }
            }
        }

        private void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            IObjectCreationOperation creation = (IObjectCreationOperation)context.Operation;
            if (creation.Type is INamedTypeSymbol type && symbols.IsClosedValue(type) && !IsInside(context.ContainingSymbol, type))
            {
                context.ReportDiagnostic(Diagnostic.Create(ClosedValueDiagnostics.CreatedOutsideType, creation.Syntax.GetLocation(), type.Name));
            }
        }

        private void AnalyzeDefaultValue(OperationAnalysisContext context)
        {
            // A class default is null, which the compiler's nullable analysis covers. A struct default is a value
            // outside the closed set.
            IDefaultValueOperation operation = (IDefaultValueOperation)context.Operation;
            if (operation.Type is INamedTypeSymbol { IsValueType: true } type && symbols.IsClosedValue(type) && !IsInside(context.ContainingSymbol, type))
            {
                context.ReportDiagnostic(Diagnostic.Create(ClosedValueDiagnostics.DefaultValue, operation.Syntax.GetLocation(), type.Name));
            }
        }

        private bool IsInside(ISymbol containingSymbol, INamedTypeSymbol type)
        {
            for (INamedTypeSymbol? current = containingSymbol as INamedTypeSymbol ?? containingSymbol.ContainingType;
                 current is not null;
                 current = current.ContainingType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, type))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsUserCode(ISymbol symbol) =>
            symbol.DeclaringSyntaxReferences.Any(reference => !symbols.IsGenerated(reference.SyntaxTree));
    }
}
