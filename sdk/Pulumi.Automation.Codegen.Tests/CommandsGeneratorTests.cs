// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.IO;
using Pulumi.Automation.Codegen;
using Xunit;

namespace Pulumi.Automation.Codegen.Tests
{
    public class CommandsGeneratorTests
    {
        private const string Namespace = "Pulumi.Automation.Interface";

        // Compiled once and shared: each test exercises the same generated API.
        private static readonly GeneratedApi Fixture = new(SpecificationTests.LoadFixture());

        private static string GenerateFromFixture()
            => CommandsGenerator.Generate(SpecificationTests.LoadFixture(), Namespace);

        [Fact]
        public void Generate_MatchesTheGoldenFile()
        {
            var source = GenerateFromFixture();
            Assert.DoesNotContain('\r', source);

            var goldenPath = Path.Combine(SpecificationTests.TestDataDirectory(), "Commands.golden.cs");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PULUMI_ACCEPT")))
            {
                File.WriteAllText(goldenPath, source);
            }

            var golden = File.ReadAllText(goldenPath).ReplaceLineEndings("\n");
            Assert.Equal(golden, source);
        }

        [Fact]
        public void Generate_IsDeterministic()
            => Assert.Equal(GenerateFromFixture(), GenerateFromFixture());

        [Fact]
        public void Cancel_WithNoArguments_EmitsOnlyTheCommandAndPreset()
            => Assert.Equal(new[] { "cancel", "--yes" }, Fixture.Invoke("Cancel", null, null));

        [Fact]
        public void Cancel_EmitsFlagsThenPositionalBehindSeparator()
        {
            var options = Fixture.Options("PulumiCancelOptions", ("Stack", "prod"));
            Assert.Equal(
                new[] { "cancel", "--yes", "--stack", "prod", "--", "dev" },
                Fixture.Invoke("Cancel", "dev", options));
        }

        [Fact]
        public void Cancel_IntFlagIsFormattedInvariantly()
        {
            var options = Fixture.Options("PulumiCancelOptions", ("Verbose", 3));
            Assert.Equal(new[] { "cancel", "--yes", "--verbose", "3" }, Fixture.Invoke("Cancel", null, options));
        }

        [Fact]
        public void Flags_AreEmittedInAlphabeticalOrderAfterPresets()
        {
            var options = Fixture.Options(
                "PulumiStateMoveOptions",
                ("Source", "s"),
                ("Dest", "d"),
                ("IncludeParents", true));
            Assert.Equal(
                new[] { "state", "move", "--yes", "--dest", "d", "--include-parents", "--source", "s" },
                Fixture.Invoke("StateMove", null, options));
        }

        [Fact]
        public void BooleanFlag_IsOmittedWhenFalse()
        {
            var options = Fixture.Options("PulumiStateMoveOptions", ("IncludeParents", false));
            Assert.Equal(new[] { "state", "move", "--yes" }, Fixture.Invoke("StateMove", null, options));
        }

        [Fact]
        public void RepeatableFlag_IsEmittedOncePerItem()
        {
            var options = Fixture.Options("PulumiOrgSearchOptions", ("Query", new List<string> { "a", "b" }));
            Assert.Equal(
                new[] { "org", "search", "--query", "a", "--query", "b" },
                Fixture.Invoke("OrgSearch", options));
        }

        [Fact]
        public void RedefinedFlag_UsesTheClosestDeclaration()
        {
            // `org search` declares --query repeatable; `org search ai` redefines
            // it as a scalar, so the leaf command emits it once.
            var options = Fixture.Options("PulumiOrgSearchAIOptions", ("Query", "q"));
            Assert.Equal(new[] { "org", "search", "ai", "--query", "q" }, Fixture.Invoke("OrgSearchAI", options));
        }

        [Fact]
        public void VariadicPositional_ExpandsBehindSeparator()
        {
            var options = Fixture.Options("PulumiStateMoveOptions", ("Dest", "d"));
            Assert.Equal(
                new[] { "state", "move", "--yes", "--dest", "d", "--", "urn1", "urn2" },
                Fixture.Invoke("StateMove", new List<string> { "urn1", "urn2" }, options));
        }

        [Fact]
        public void Positionals_AreOmittedWhenEmpty()
            => Assert.Equal(new[] { "state", "move", "--yes" }, Fixture.Invoke("StateMove", null, null));

        [Fact]
        public void RequiredPositional_IsAlwaysEmitted()
            => Assert.Equal(new[] { "org", "set-default", "--", "acme" }, Fixture.Invoke("OrgSetDefault", "acme", null));

        [Fact]
        public void OmittedPresetString_IsInjectedUnconditionally()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""deploy"": {
                        ""type"": ""command"",
                        ""flags"": { ""color"": { ""name"": ""color"", ""type"": ""string"", ""omit"": true, ""preset"": ""never"" } }
                    }
                }
            }";

            var source = CommandsGenerator.Generate(Specification.Parse(json), Namespace);
            Assert.Contains("__final.Add(\"--color\");", source);
            Assert.Contains("__final.Add(\"never\");", source);
            Assert.DoesNotContain("options?.Color", source);
        }

        [Fact]
        public void OmittedPresetArray_IsInjectedPerItem()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""deploy"": {
                        ""type"": ""command"",
                        ""flags"": { ""tag"": { ""name"": ""tag"", ""type"": ""string"", ""repeatable"": true, ""omit"": true, ""preset"": [""a"", ""b""] } }
                    }
                }
            }";

            var api = new GeneratedApi(Specification.Parse(json));
            Assert.Equal(
                new[] { "deploy", "--tag", "a", "--tag", "b" },
                api.Invoke("Deploy", new object?[] { null }));
        }

        [Fact]
        public void UserOverridablePreset_IsAppliedOnlyWhenUnset()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""deploy"": {
                        ""type"": ""command"",
                        ""flags"": { ""keep"": { ""name"": ""keep"", ""type"": ""boolean"", ""preset"": true } }
                    }
                }
            }";

            var source = CommandsGenerator.Generate(Specification.Parse(json), Namespace);
            // Preset applied when the user did not set the flag...
            Assert.Contains("if (options?.Keep is null)", source);
            // ...and the user's own value still flows through the option path.
            Assert.Contains("if (options?.Keep is true)", source);
        }

        [Fact]
        public void Generate_RejectsPositionalsNamedLikeTheOptionsParameter()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""x"": { ""type"": ""command"", ""arguments"": { ""arguments"": [{ ""name"": ""options"" }] } }
                }
            }";

            var exception = Assert.Throws<InvalidOperationException>(
                () => CommandsGenerator.Generate(Specification.Parse(json), Namespace));
            Assert.Contains("options", exception.Message);
        }

        [Fact]
        public void Generate_RejectsCollidingMethodNames()
        {
            const string json = @"{
                ""type"": ""menu"",
                ""commands"": {
                    ""get-default"": { ""type"": ""command"" },
                    ""get"": { ""type"": ""menu"", ""commands"": { ""default"": { ""type"": ""command"" } } }
                }
            }";

            var exception = Assert.Throws<InvalidOperationException>(
                () => CommandsGenerator.Generate(Specification.Parse(json), Namespace));
            Assert.Contains("GetDefault", exception.Message);
        }
    }
}
