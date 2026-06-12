// Copyright 2024, Pulumi Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Pulumi.Esc.Sdk.Client;
using Pulumi.Esc.Sdk.Model;
using Xunit;

namespace Pulumi.Esc.Sdk.Tests
{
    /// <summary>
    /// End-to-end integration tests for the ESC C# SDK.
    /// Requires PULUMI_ACCESS_TOKEN and PULUMI_ORG environment variables to be set.
    /// Mirrors the Go test in sdk/go/api_esc_test.go.
    /// </summary>
    [Trait("Category", "Integration")]
    public class EscApiTests : IAsyncLifetime
    {
        private const string ProjectName = "sdk-csharp-test";
        private const string CloneProjectName = ProjectName + "-clone";
        private const string EnvPrefix = "env-";

        private readonly string _orgName;
        private EscClient _client = null!;
        private string _baseEnvName = null!;

        public EscApiTests()
        {
            _orgName = Environment.GetEnvironmentVariable("PULUMI_ORG")
                ?? throw new InvalidOperationException("PULUMI_ORG must be set");
        }

        public async Task InitializeAsync()
        {
            _client = EscClient.CreateDefault();
            await RemoveAllCSharpTestEnvsAsync();

            _baseEnvName = "base-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            await _client.CreateEnvironmentAsync(_orgName, ProjectName, _baseEnvName);

            var baseYaml = $"values:\n  base: {_baseEnvName}\n";
            await _client.UpdateEnvironmentYamlAsync(_orgName, ProjectName, _baseEnvName, baseYaml);
        }

        public async Task DisposeAsync()
        {
            await SafeDelete(_orgName, ProjectName, _baseEnvName);
            _client.Dispose();
        }

        [Fact]
        public async Task FullLifecycle_Create_Clone_List_Update_Get_Decrypt_Open_Tags_Delete()
        {
            var envName = EnvPrefix + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var cloneName = $"{envName}-clone";

            try
            {
                // -- Create and clone environment --
                await _client.CreateEnvironmentAsync(_orgName, ProjectName, envName);
                await _client.CloneEnvironmentAsync(_orgName, ProjectName, envName, CloneProjectName, cloneName);

                // -- Verify created and cloned environments are queryable --
                await _client.GetEnvironmentAsync(_orgName, ProjectName, envName);
                await _client.GetEnvironmentAsync(_orgName, CloneProjectName, cloneName);

                // -- Open and read (empty env should have no values) --
                var (_, emptyValues) = await _client.OpenAndReadEnvironmentAsync(_orgName, ProjectName, envName);
                Assert.Null(emptyValues);

                // -- Update with YAML --
                var yaml = $"imports:\n  - {ProjectName}/{_baseEnvName}\n" +
@"values:
  foo: bar
  my_secret:
    fn::secret: ""shh! don't tell anyone""
  my_array: [1, 2, 3]
  pulumiConfig:
    foo: ${foo}
  environmentVariables:
    FOO: ${foo}
";
                var diags = await _client.UpdateEnvironmentYamlAsync(_orgName, ProjectName, envName, yaml);
                Assert.NotNull(diags);
                Assert.Empty(diags!.Diagnostics ?? new List<EnvironmentDiagnostic>());

                // -- GetEnvironment (parsed from YAML) --
                var envDef = await _client.GetEnvironmentAsync(_orgName, ProjectName, envName);
                AssertEnvDef(envDef!, _baseEnvName);
                // my_secret should be present as additional property
                Assert.True(envDef!.Values!.AdditionalProperties.ContainsKey("my_secret"));

                // -- DecryptEnvironment --
                var decryptedDef = await _client.DecryptEnvironmentAsync(_orgName, ProjectName, envName);
                Assert.NotNull(decryptedDef);
                AssertEnvDef(decryptedDef!, _baseEnvName);
                // Decrypted should have the plaintext secret in additional properties
                Assert.True(decryptedDef!.Values!.AdditionalProperties.ContainsKey("my_secret"));
                var mySecret = decryptedDef.Values.AdditionalProperties["my_secret"];
                Assert.Equal(JsonValueKind.Object, mySecret.ValueKind);
                Assert.Equal("shh! don't tell anyone", mySecret.GetProperty("fn::secret").GetString());

                // -- Open and read (should have resolved values) --
                var (env, resolvedValues) = await _client.OpenAndReadEnvironmentAsync(_orgName, ProjectName, envName);
                Assert.NotNull(resolvedValues);
                Assert.Equal(_baseEnvName, resolvedValues!["base"]);
                Assert.Equal("bar", resolvedValues["foo"]);
                Assert.Equal(new List<object> { 1.0, 2.0, 3.0 }, resolvedValues["my_array"]);
                Assert.Equal("shh! don't tell anyone", resolvedValues["my_secret"]);
                var pulumiConfig = (Dictionary<string, object?>)resolvedValues["pulumiConfig"]!;
                Assert.Equal("bar", pulumiConfig["foo"]);
                var environmentVariables = (Dictionary<string, object?>)resolvedValues["environmentVariables"]!;
                Assert.Equal("bar", environmentVariables["FOO"]);

                // -- Read property --
                var (openSessionId, _) = await _client.OpenEnvironmentAsync(_orgName, ProjectName, envName);
                var (propValue, propPrimitive) = await _client.ReadOpenEnvironmentPropertyAsync(
                    _orgName, ProjectName, envName, openSessionId, "pulumiConfig.foo");
                Assert.Equal("bar", propValue.VarValue);
                Assert.Equal("bar", propPrimitive);

                // -- GetEnvironmentAtVersion, add a property, UpdateEnvironment --
                var envDefV2 = await _client.GetEnvironmentAtVersionAsync(_orgName, ProjectName, envName, "2");
                Assert.NotNull(envDefV2);
                envDefV2!.Values!.AdditionalProperties["versioned"] = JsonDocument.Parse("\"true\"").RootElement.Clone();
                await _client.UpdateEnvironmentAsync(_orgName, ProjectName, envName, envDefV2);

                // -- Revisions (should now have 3) --
                var revisions = await _client.ListEnvironmentRevisionsAsync(_orgName, ProjectName, envName);
                Assert.Equal(3, revisions.Count);

                // -- Revision tags --
                await _client.CreateEnvironmentRevisionTagAsync(_orgName, ProjectName, envName, "testTag", 2);

                // OpenAndReadEnvironmentAtVersion with testTag (pointing to rev 2, before "versioned" was added)
                var (_, tagValues) = await _client.OpenAndReadEnvironmentAtVersionAsync(_orgName, ProjectName, envName, "testTag");
                Assert.NotNull(tagValues);
                Assert.False(tagValues!.ContainsKey("versioned"));

                var revTags = await _client.ListEnvironmentRevisionTagsAsync(_orgName, ProjectName, envName);
                Assert.Equal(2, revTags.Tags!.Count);
                Assert.Equal("latest", revTags.Tags[0].Name);
                Assert.Equal("testTag", revTags.Tags[1].Name);

                // Update revision tag to point to rev 3
                await _client.UpdateEnvironmentRevisionTagAsync(_orgName, ProjectName, envName, "testTag", 3);

                var (_, tagValues2) = await _client.OpenAndReadEnvironmentAtVersionAsync(_orgName, ProjectName, envName, "testTag");
                Assert.NotNull(tagValues2);
                Assert.Equal("true", tagValues2!["versioned"]);

                var testTag = await _client.GetEnvironmentRevisionTagAsync(_orgName, ProjectName, envName, "testTag");
                Assert.Equal(3, testTag.Revision);

                await _client.DeleteEnvironmentRevisionTagAsync(_orgName, ProjectName, envName, "testTag");
                revTags = await _client.ListEnvironmentRevisionTagsAsync(_orgName, ProjectName, envName);
                Assert.Single(revTags.Tags!);

                // -- Environment tags --
                await _client.CreateEnvironmentTagAsync(_orgName, ProjectName, envName, "owner", "esc-sdk-test");

                var envTags = await _client.ListEnvironmentTagsAsync(_orgName, ProjectName, envName);
                Assert.NotNull(envTags.Tags);
                Assert.Single(envTags.Tags!);
                Assert.Equal("owner", envTags.Tags!["owner"].Name);
                Assert.Equal("esc-sdk-test", envTags.Tags["owner"].Value);

                await _client.UpdateEnvironmentTagAsync(_orgName, ProjectName, envName,
                    "owner", "esc-sdk-test", "new-owner", "esc-sdk-test-updated");

                var envTag = await _client.GetEnvironmentTagAsync(_orgName, ProjectName, envName, "new-owner");
                Assert.Equal("new-owner", envTag.Name);
                Assert.Equal("esc-sdk-test-updated", envTag.Value);

                await _client.DeleteEnvironmentTagAsync(_orgName, ProjectName, envName, "new-owner");
                envTags = await _client.ListEnvironmentTagsAsync(_orgName, ProjectName, envName);
                Assert.Empty(envTags.Tags!);
            }
            finally
            {
                await SafeDelete(_orgName, ProjectName, envName);
                await SafeDelete(_orgName, CloneProjectName, cloneName);
            }
        }

        [Fact]
        public async Task CheckEnvironment_Valid()
        {
            var definition = new EnvironmentDefinition(
                values: new Option<EnvironmentDefinitionValues?>(new EnvironmentDefinitionValues()));
            definition.Values!.AdditionalProperties["foo"] =
                JsonDocument.Parse("\"bar\"").RootElement.Clone();

            var result = await _client.CheckEnvironmentAsync(_orgName, definition);
            Assert.NotNull(result);
            Assert.True(
                result!.Diagnostics == null || result.Diagnostics.Count == 0,
                "Expected no diagnostics for valid definition");
        }

        [Fact]
        public async Task CheckEnvironmentYaml_Invalid()
        {
            var yaml = @"
values:
  foo: bar
  pulumiConfig:
    foo: ${bad_ref}
";
            var result = await _client.CheckEnvironmentYamlAsync(_orgName, yaml);
            Assert.NotNull(result);
            Assert.NotNull(result!.Diagnostics);
            Assert.Single(result.Diagnostics!);
            Assert.Equal("unknown property \"bad_ref\"", result.Diagnostics[0].Summary);
        }

        #region Helpers

        private void AssertEnvDef(EnvironmentDefinition envDef, string baseEnvName)
        {
            Assert.NotNull(envDef.Imports);
            Assert.Single(envDef.Imports!);
            Assert.Equal($"{ProjectName}/{baseEnvName}", envDef.Imports[0]);

            Assert.NotNull(envDef.Values);
            Assert.Equal("bar", envDef.Values!.AdditionalProperties["foo"].GetString());

            var myArray = envDef.Values.AdditionalProperties["my_array"];
            Assert.Equal(JsonValueKind.Array, myArray.ValueKind);
            Assert.Equal(3, myArray.GetArrayLength());

            Assert.NotNull(envDef.Values.PulumiConfig);
            Assert.Equal("${foo}", envDef.Values.PulumiConfig!["foo"]);

            Assert.NotNull(envDef.Values.EnvironmentVariables);
            Assert.Equal("${foo}", envDef.Values.EnvironmentVariables!["FOO"]);
        }

        private async Task SafeDelete(string orgName, string projectName, string envName)
        {
            try
            {
                await _client.DeleteEnvironmentAsync(orgName, projectName, envName);
            }
            catch
            {
                // Ignore — cleanup is best-effort
            }
        }

        private async Task RemoveAllCSharpTestEnvsAsync()
        {
            string? continuationToken = null;
            do
            {
                var envs = await _client.ListEnvironmentsAsync(_orgName, continuationToken);
                foreach (var env in envs.Environments ?? new List<OrgEnvironment>())
                {
                    if ((env.Project == ProjectName || env.Project == CloneProjectName) &&
                        (env.Name.StartsWith(EnvPrefix) || env.Name.StartsWith("base-")))
                    {
                        await SafeDelete(_orgName, env.Project!, env.Name);
                    }
                }
                continuationToken = envs.NextToken;
            } while (!string.IsNullOrEmpty(continuationToken));
        }

        #endregion
    }
}
