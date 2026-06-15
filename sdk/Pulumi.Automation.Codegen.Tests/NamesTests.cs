// Copyright 2016-2026, Pulumi Corporation

using Pulumi.Automation.Codegen;
using Xunit;

namespace Pulumi.Automation.Codegen.Tests
{
    public class NamesTests
    {
        [Theory]
        [InlineData("color", "Color")]
        [InlineData("include-parents", "IncludeParents")]
        [InlineData("stack-name", "StackName")]
        [InlineData("show_secrets", "ShowSecrets")]
        [InlineData("get-default", "GetDefault")]
        [InlineData("otel-traces", "OtelTraces")]
        [InlineData("ai", "AI")]
        [InlineData("ai-helper", "AIHelper")]
        public void PascalCase_ConvertsKebabCaseNames(string name, string expected)
            => Assert.Equal(expected, Names.PascalCase(name));

        [Fact]
        public void PascalCase_ConcatenatesBreadcrumbs()
        {
            Assert.Equal("OrgSearchAI", Names.PascalCase(new[] { "org", "search", "ai" }));
            Assert.Equal("StateMove", Names.PascalCase(new[] { "state", "move" }));
            Assert.Equal("OrgGetDefault", Names.PascalCase(new[] { "org", "get-default" }));
        }

        [Theory]
        [InlineData("stack-name", "stackName")]
        [InlineData("include-parents", "includeParents")]
        [InlineData("color", "color")]
        [InlineData("ai", "ai")]
        [InlineData("ai-helper", "aiHelper")]
        [InlineData("new", "@new")]
        [InlineData("default", "@default")]
        [InlineData("event", "@event")]
        public void CamelCase_ConvertsKebabCaseNamesAndEscapesKeywords(string name, string expected)
            => Assert.Equal(expected, Names.CamelCase(name));

        [Theory]
        [InlineData("class", "@class")]
        [InlineData("params", "@params")]
        [InlineData("color", "color")]
        // Contextual keywords like "var" are valid identifiers and need no escaping.
        [InlineData("var", "var")]
        public void EscapeIdentifier_EscapesReservedKeywordsOnly(string identifier, string expected)
            => Assert.Equal(expected, Names.EscapeIdentifier(identifier));
    }
}
