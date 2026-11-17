using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace ClosedValues
{
    /// <summary>
    /// The rules that keep a closed value type closed.
    /// </summary>
    internal static class ClosedValueDiagnostics
    {
        private const string Category = "ClosedValues";

        private static string? HelpLink(string id)
        {
            string? repositoryUrl = typeof(ClosedValueDiagnostics).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "RepositoryUrl")
                ?.Value;

            return string.IsNullOrEmpty(repositoryUrl) ? null : $"{repositoryUrl!.TrimEnd('/')}/blob/main/docs/rules/{id}.md";
        }

        public static readonly DiagnosticDescriptor CreatedOutsideType = new(
            "CV0001",
            "Closed value created outside its type",
            "Create '{0}' only inside its own type. Use one of its static instances instead.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink("CV0001"));

        public static readonly DiagnosticDescriptor DeclaresConstructor = new(
            "CV0002",
            "Closed value declares a constructor",
            "Remove the constructor from '{0}'. The generator writes the private constructor.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink("CV0002"));

        public static readonly DiagnosticDescriptor NotPartial = new(
            "CV0003",
            "Closed value is not partial",
            "Make '{0}' partial, so that the generator can add its members",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink("CV0003"));

        public static readonly DiagnosticDescriptor NotSealed = new(
            "CV0004",
            "Closed value is not sealed",
            "Make '{0}' sealed. A derived class can add values to a closed set.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink("CV0004"));

        public static readonly DiagnosticDescriptor MutableState = new(
            "CV0005",
            "Closed value has mutable state",
            "Make '{0}' a get-only auto-property. A closed value holds its state in get-only auto-properties only.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink("CV0005"));

        public static readonly DiagnosticDescriptor ReservedMemberName = new(
            "CV0008",
            "Closed value declares a generated member",
            "Rename '{0}'. The generator writes a member with this name.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink("CV0008"));

        public static readonly DiagnosticDescriptor DefaultValue = new(
            "CV0006",
            "Closed value struct created with default",
            "Do not use the default value of '{0}' outside its own type. Use one of its static instances instead.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink("CV0006"));

        public static readonly DiagnosticDescriptor UnsupportedShape = new(
            "CV0007",
            "Closed value has an unsupported shape",
            "Make '{0}' a top-level, non-generic, non-static class, record or struct. A ref struct is not supported.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink("CV0007"));
    }
}
