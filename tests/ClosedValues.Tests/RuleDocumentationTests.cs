using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ClosedValues.Tests
{
    public sealed class RuleDocumentationTests
    {
        [Fact]
        public void EveryRule_HasADocumentationPage()
        {
            string rulesDirectory = Path.Combine(FindRepositoryRoot(), "docs", "rules");

            string[] missing = new ClosedValueAnalyzer().SupportedDiagnostics
                .Select(descriptor => descriptor.Id)
                .Where(id => !File.Exists(Path.Combine(rulesDirectory, id + ".md")))
                .ToArray();

            Assert.Empty(missing);
        }

        [Fact]
        public void EveryHelpLink_PointsToTheDocumentationPage()
        {
            foreach (DiagnosticDescriptor descriptor in new ClosedValueAnalyzer().SupportedDiagnostics)
            {
                Assert.True(
                    string.IsNullOrEmpty(descriptor.HelpLinkUri) || descriptor.HelpLinkUri.EndsWith($"/docs/rules/{descriptor.Id}.md", StringComparison.Ordinal),
                    $"{descriptor.Id} has the help link '{descriptor.HelpLinkUri}'.");
            }
        }

        private string FindRepositoryRoot()
        {
            for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ClosedValues.slnx")))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException("The repository root with ClosedValues.slnx is not above " + AppContext.BaseDirectory + ".");
        }
    }
}
