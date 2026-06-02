// Copyright 2024, Pulumi Corporation.  All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using Pulumi.Esc.Sdk.Client;
using Pulumi.Esc.Sdk.Model;
using Xunit;

namespace Pulumi.Esc.Sdk.Tests
{
    /// <summary>
    /// Unit tests for <see cref="EnvironmentDefinitionSerializer"/>.
    /// </summary>
    public class EnvironmentDefinitionSerializerTests
    {
        [Fact]
        public void Deserialize_Null_ReturnsNull()
        {
            Assert.Null(EnvironmentDefinitionSerializer.Deserialize(null));
        }

        [Fact]
        public void Deserialize_Empty_ReturnsNull()
        {
            Assert.Null(EnvironmentDefinitionSerializer.Deserialize(""));
            Assert.Null(EnvironmentDefinitionSerializer.Deserialize("   "));
        }

        [Fact]
        public void Deserialize_ValuesOnly()
        {
            var yaml = @"
values:
  environmentVariables:
    FOO: bar
    BAZ: qux
  pulumiConfig:
    aws:region: us-west-2
";

            var result = EnvironmentDefinitionSerializer.Deserialize(yaml);
            Assert.NotNull(result);
            Assert.NotNull(result!.Values);

            var envVars = result.Values!.EnvironmentVariables;
            Assert.NotNull(envVars);
            Assert.Equal("bar", envVars!["FOO"]);
            Assert.Equal("qux", envVars["BAZ"]);

            var pulumiConfig = result.Values.PulumiConfig;
            Assert.NotNull(pulumiConfig);
            Assert.Equal("us-west-2", pulumiConfig!["aws:region"]);
        }

        [Fact]
        public void Deserialize_WithImports()
        {
            var yaml = @"
imports:
  - myproject/base-env
  - myproject/secrets
values:
  environmentVariables:
    APP_ENV: production
";

            var result = EnvironmentDefinitionSerializer.Deserialize(yaml);
            Assert.NotNull(result);

            Assert.NotNull(result!.Imports);
            Assert.Equal(2, result.Imports!.Count);
            Assert.Equal("myproject/base-env", result.Imports[0]);
            Assert.Equal("myproject/secrets", result.Imports[1]);

            Assert.NotNull(result.Values?.EnvironmentVariables);
            Assert.Equal("production", result.Values!.EnvironmentVariables!["APP_ENV"]);
        }

        [Fact]
        public void Deserialize_WithFiles()
        {
            var yaml = @"
values:
  files:
    KUBECONFIG: contents-here
";

            var result = EnvironmentDefinitionSerializer.Deserialize(yaml);
            Assert.NotNull(result);
            Assert.NotNull(result!.Values?.Files);
            Assert.Equal("contents-here", result.Values!.Files!["KUBECONFIG"]);
        }

        [Fact]
        public void Deserialize_EmptyValues()
        {
            var yaml = @"
values: {}
";

            var result = EnvironmentDefinitionSerializer.Deserialize(yaml);
            // The YAML parser may return an empty dict for values: {}
            // which won't match the Dictionary<object, object> type check
            Assert.NotNull(result);
        }

        [Fact]
        public void Serialize_ProducesValidJson()
        {
            var definition = new EnvironmentDefinition(
                imports: new Option<List<string>?>(new List<string> { "project/base" }),
                values: new Option<EnvironmentDefinitionValues?>(new EnvironmentDefinitionValues(
                    environmentVariables: new Option<Dictionary<string, string>?>(new Dictionary<string, string>
                    {
                        ["FOO"] = "bar",
                    }),
                    files: default,
                    pulumiConfig: default
                ))
            );

            var json = EnvironmentDefinitionSerializer.Serialize(definition);
            Assert.Contains("\"imports\"", json);
            Assert.Contains("\"project/base\"", json);
            Assert.Contains("\"FOO\"", json);
            Assert.Contains("\"bar\"", json);
        }

        [Fact]
        public void SerializeToYaml_ProducesValidYaml()
        {
            var definition = new EnvironmentDefinition(
                imports: new Option<List<string>?>(new List<string> { "project/base" }),
                values: new Option<EnvironmentDefinitionValues?>(new EnvironmentDefinitionValues(
                    environmentVariables: new Option<Dictionary<string, string>?>(new Dictionary<string, string>
                    {
                        ["FOO"] = "bar",
                    }),
                    files: default,
                    pulumiConfig: default
                ))
            );

            var yaml = EnvironmentDefinitionSerializer.SerializeToYaml(definition);
            Assert.Contains("imports:", yaml);
            Assert.Contains("project/base", yaml);
            Assert.Contains("environmentVariables:", yaml);
            Assert.Contains("FOO: bar", yaml);
        }

        [Fact]
        public void RoundTrip_DeserializeThenSerializeToYaml()
        {
            var originalYaml = @"
imports:
  - myproject/base
values:
  environmentVariables:
    DB_HOST: localhost
    DB_PORT: ""5432""
  pulumiConfig:
    aws:region: us-east-1
";

            var definition = EnvironmentDefinitionSerializer.Deserialize(originalYaml);
            Assert.NotNull(definition);

            var roundTripped = EnvironmentDefinitionSerializer.SerializeToYaml(definition!);
            Assert.Contains("imports:", roundTripped);
            Assert.Contains("myproject/base", roundTripped);
            Assert.Contains("environmentVariables:", roundTripped);
            Assert.Contains("DB_HOST: localhost", roundTripped);
            Assert.Contains("DB_PORT:", roundTripped);
            Assert.Contains("pulumiConfig:", roundTripped);
            Assert.Contains("aws:region: us-east-1", roundTripped);
        }

        [Fact]
        public void Deserialize_AdditionalProperties_AreCaptured()
        {
            var yaml = @"
imports:
  - myproject/base
values:
  foo: bar
  my_secret:
    fn::secret: ""shh""
  my_array: [1, 2, 3]
  pulumiConfig:
    foo: ${foo}
  environmentVariables:
    FOO: ${foo}
";

            var result = EnvironmentDefinitionSerializer.Deserialize(yaml);
            Assert.NotNull(result);
            Assert.NotNull(result!.Values);

            // Typed properties
            Assert.NotNull(result.Values!.PulumiConfig);
            Assert.Equal("${foo}", result.Values.PulumiConfig!["foo"]);
            Assert.NotNull(result.Values.EnvironmentVariables);
            Assert.Equal("${foo}", result.Values.EnvironmentVariables!["FOO"]);

            // Additional properties
            var additional = result.Values.AdditionalProperties;
            Assert.True(additional.ContainsKey("foo"));
            Assert.Equal(JsonValueKind.String, additional["foo"].ValueKind);
            Assert.Equal("bar", additional["foo"].GetString());

            Assert.True(additional.ContainsKey("my_secret"));
            Assert.Equal(JsonValueKind.Object, additional["my_secret"].ValueKind);

            Assert.True(additional.ContainsKey("my_array"));
            Assert.Equal(JsonValueKind.Array, additional["my_array"].ValueKind);
            Assert.Equal(3, additional["my_array"].GetArrayLength());
        }

        [Fact]
        public void SerializeToYaml_AdditionalProperties_AreIncluded()
        {
            var values = new EnvironmentDefinitionValues(
                pulumiConfig: new Option<Dictionary<string, object>?>(new Dictionary<string, object>
                {
                    ["region"] = "us-west-2",
                })
            );

            // Add additional properties (simulating what deserialization produces)
            values.AdditionalProperties["foo"] = ToJsonElement("bar");
            values.AdditionalProperties["count"] = ToJsonElement(42);
            values.AdditionalProperties["items"] = ToJsonElement(new[] { 1, 2, 3 });

            var definition = new EnvironmentDefinition(
                values: new Option<EnvironmentDefinitionValues?>(values));

            var yaml = EnvironmentDefinitionSerializer.SerializeToYaml(definition);
            Assert.Contains("pulumiConfig:", yaml);
            Assert.Contains("region: us-west-2", yaml);
            Assert.Contains("foo: bar", yaml);
            Assert.Contains("count: 42", yaml);
            Assert.Contains("items:", yaml);
        }

        [Fact]
        public void RoundTrip_AdditionalProperties_PreservedThroughDeserializeSerialize()
        {
            var originalYaml = @"
imports:
  - myproject/base
values:
  foo: bar
  my_secret:
    fn::secret: ""shh! don't tell anyone""
  my_array: [1, 2, 3]
  pulumiConfig:
    foo: ${foo}
  environmentVariables:
    FOO: ${foo}
";

            var definition = EnvironmentDefinitionSerializer.Deserialize(originalYaml);
            Assert.NotNull(definition);

            var roundTripped = EnvironmentDefinitionSerializer.SerializeToYaml(definition!);

            // Typed properties
            Assert.Contains("pulumiConfig:", roundTripped);
            Assert.Contains("environmentVariables:", roundTripped);
            Assert.Contains("imports:", roundTripped);
            Assert.Contains("myproject/base", roundTripped);

            // Additional properties survived the round-trip
            Assert.Contains("foo: bar", roundTripped);
            Assert.Contains("fn::secret:", roundTripped);
            Assert.Contains("my_array:", roundTripped);
        }

        private static JsonElement ToJsonElement<T>(T value)
        {
            var json = JsonSerializer.Serialize(value);
            return JsonDocument.Parse(json).RootElement.Clone();
        }
    }
}
