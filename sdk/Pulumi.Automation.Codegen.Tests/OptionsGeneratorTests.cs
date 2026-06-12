// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pulumi.Automation.Codegen;
using Xunit;

namespace Pulumi.Automation.Codegen.Tests
{
    public class OptionsGeneratorTests
    {
        private const string Namespace = "Pulumi.Automation.Interface";

        private static string GenerateFromFixture()
            => OptionsGenerator.Generate(SpecificationTests.LoadFixture(), Namespace);

        [Fact]
        public void Generate_MatchesTheGoldenFile()
        {
            var source = GenerateFromFixture();
            Assert.DoesNotContain('\r', source);

            var goldenPath = Path.Combine(SpecificationTests.TestDataDirectory(), "Options.golden.cs");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PULUMI_ACCEPT")))
            {
                File.WriteAllText(goldenPath, source);
            }

            // Normalize the golden file in case git checked it out with CRLF
            // line endings; the generator always emits LF.
            var golden = File.ReadAllText(goldenPath).ReplaceLineEndings("\n");
            Assert.Equal(golden, source);
        }

        [Fact]
        public void Generate_IsDeterministic()
            => Assert.Equal(GenerateFromFixture(), GenerateFromFixture());

        [Fact]
        public void Generate_ProducesCompilableSource()
        {
            var tree = CSharpSyntaxTree.ParseText(GenerateFromFixture());
            var compilation = CSharpCompilation.Create(
                "Pulumi.Automation.Codegen.Generated",
                new[] { tree },
                References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var errors = compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToList();
            Assert.Empty(errors);
        }

        [Fact]
        public void Generate_StripsOverridesOnDescent()
        {
            // color is omitted at the root, but the omission must not leak to
            // descendants through inheritance: every options class exposes it.
            var source = GenerateFromFixture();
            Assert.DoesNotContain("PulumiOptions", source);
            Assert.Contains("public string? Color { get; set; }", source);
        }

        [Fact]
        public void Generate_DoesNotExposeOmittedPresetFlags()
        {
            // cancel's --yes flag is omitted with a preset; it must not
            // surface as a property anywhere.
            var source = GenerateFromFixture();
            Assert.DoesNotContain("Yes", source);
        }

        [Fact]
        public void Generate_LetsLocalFlagsRedefineInheritedOnes()
        {
            // The root declares a repeatable --q; the x command redefines it
            // as a plain string, and the closest declaration wins.
            const string json = @"{
                ""type"": ""menu"",
                ""flags"": { ""q"": { ""name"": ""q"", ""type"": ""string"", ""repeatable"": true } },
                ""commands"": {
                    ""x"": { ""type"": ""command"", ""flags"": { ""q"": { ""name"": ""q"", ""type"": ""string"" } } },
                    ""y"": { ""type"": ""command"" }
                }
            }";

            var source = OptionsGenerator.Generate(Specification.Parse(json), Namespace);

            var x = ClassBody(source, "PulumiXOptions");
            var y = ClassBody(source, "PulumiYOptions");
            Assert.Contains("public string? Q { get; set; }", x);
            Assert.Contains("public List<string>? Q { get; set; }", y);
        }

        [Fact]
        public void Generate_SkipsNonExecutableMenusAndIncludesExecutableOnes()
        {
            var source = GenerateFromFixture();

            // state is a plain menu, org and org search are executable menus.
            Assert.DoesNotContain("class PulumiStateOptions", source);
            Assert.Contains("class PulumiOrgOptions", source);
            Assert.Contains("class PulumiOrgSearchOptions", source);
            Assert.Contains("class PulumiOrgSearchAIOptions", source);
        }

        [Fact]
        public void Generate_RejectsAnExecutableRoot()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => OptionsGenerator.Generate(Specification.Parse(@"{ ""type"": ""menu"", ""executable"": true }"), Namespace));
            Assert.Contains("root", exception.Message);
        }

        [Fact]
        public void Generate_RejectsCollidingClassNames()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""get-default"": { ""type"": ""command"" },
                    ""get"": { ""type"": ""menu"", ""commands"": { ""default"": { ""type"": ""command"" } } }
                }
            }";

            var exception = Assert.Throws<InvalidOperationException>(
                () => OptionsGenerator.Generate(Specification.Parse(json), Namespace));
            Assert.Contains("PulumiGetDefaultOptions", exception.Message);
        }

        [Fact]
        public void Generate_RejectsCollidingPropertyNames()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""x"": {
                        ""type"": ""command"",
                        ""flags"": {
                            ""show-secrets"": { ""name"": ""show-secrets"", ""type"": ""boolean"" },
                            ""show_secrets"": { ""name"": ""show_secrets"", ""type"": ""boolean"" }
                        }
                    }
                }
            }";

            var exception = Assert.Throws<InvalidOperationException>(
                () => OptionsGenerator.Generate(Specification.Parse(json), Namespace));
            Assert.Contains("ShowSecrets", exception.Message);
        }

        [Fact]
        public void Generate_RejectsPropertiesNamedLikeTheirClass()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""x"": {
                        ""type"": ""command"",
                        ""flags"": { ""pulumi-x-options"": { ""name"": ""pulumi-x-options"", ""type"": ""string"" } }
                    }
                }
            }";

            var exception = Assert.Throws<InvalidOperationException>(
                () => OptionsGenerator.Generate(Specification.Parse(json), Namespace));
            Assert.Contains("PulumiXOptions", exception.Message);
        }

        [Fact]
        public void Generate_RejectsUnknownFlagTypes()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""x"": {
                        ""type"": ""command"",
                        ""flags"": { ""f"": { ""name"": ""f"", ""type"": ""float"" } }
                    }
                }
            }";

            var exception = Assert.Throws<InvalidOperationException>(
                () => OptionsGenerator.Generate(Specification.Parse(json), Namespace));
            Assert.Contains("float", exception.Message);
        }

        [Fact]
        public void Generate_EscapesXmlInDescriptions()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""x"": {
                        ""type"": ""command"",
                        ""flags"": {
                            ""f"": { ""name"": ""f"", ""type"": ""string"", ""description"": ""Limit to 'plugin:<name>' & more"" }
                        }
                    }
                }
            }";

            var source = OptionsGenerator.Generate(Specification.Parse(json), Namespace);
            Assert.Contains("/// Limit to 'plugin:&lt;name&gt;' &amp; more", source);
        }

        private static string ClassBody(string source, string className)
        {
            var start = source.IndexOf($"class {className}", StringComparison.Ordinal);
            Assert.True(start >= 0, $"Expected to find class {className} in the generated source.");
            var end = source.IndexOf("    }", start, StringComparison.Ordinal);
            return source[start..end];
        }

        private static IReadOnlyList<MetadataReference> References()
        {
            var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System.Private.CoreLib",
                "System.Runtime",
                "System.Collections",
                "netstandard",
            };

            return trustedAssemblies.Split(Path.PathSeparator)
                .Where(path => wanted.Contains(Path.GetFileNameWithoutExtension(path)))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();
        }
    }
}
