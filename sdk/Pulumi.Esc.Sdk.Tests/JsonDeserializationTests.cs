// Copyright 2026, Pulumi Corporation.  All rights reserved.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Pulumi.Esc.Sdk.Client;
using Pulumi.Esc.Sdk.Model;
using Xunit;

namespace Pulumi.Esc.Sdk.Tests
{
    /// <summary>
    /// Tests for JSON deserialization paths reported in esc-sdk-csharp-bugs.md.
    /// Verifies Bug 1 (OpenEnvironment TryOk) and Bug 2 (EnvironmentDiagnostics deserialization)
    /// using the same JsonSerializerOptions that HostConfiguration creates.
    /// </summary>
    public class JsonDeserializationTests
    {
        /// <summary>
        /// Build the exact same JsonSerializerOptions that HostConfiguration creates,
        /// with all the custom converters registered.
        /// </summary>
        private static JsonSerializerOptions CreateHostConfigurationOptions()
        {
            // Replicate what HostConfiguration constructor does (line 31 + converter registrations)
            var services = new ServiceCollection();
            var hostConfig = new HostConfiguration(services);
            // The HostConfiguration constructor registers a JsonSerializerOptionsProvider singleton.
            // We need to resolve it to get the same options instance.
            var sp = services.BuildServiceProvider();
            return sp.GetRequiredService<JsonSerializerOptionsProvider>().Options;
        }

        #region Bug 1: OpenEnvironment deserialization

        [Fact]
        public void OpenEnvironment_Deserialize_WithHostConfigOptions_Succeeds()
        {
            // Arrange: exact JSON the API returns
            var json = @"{""id"":""e095ed11-8ff4-49c4-beee-7369ae99d14d""}";
            var options = CreateHostConfigurationOptions();

            // Act
            var result = JsonSerializer.Deserialize<OpenEnvironment>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("e095ed11-8ff4-49c4-beee-7369ae99d14d", result!.Id);
            Assert.Null(result.Diagnostics);
        }

        [Fact]
        public void OpenEnvironment_Deserialize_WithDiagnostics_Succeeds()
        {
            // Arrange: JSON with diagnostics
            var json = @"{""id"":""abc123"",""diagnostics"":{""diagnostics"":[]}}";
            var options = CreateHostConfigurationOptions();

            // Act
            var result = JsonSerializer.Deserialize<OpenEnvironment>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("abc123", result!.Id);
            Assert.NotNull(result.Diagnostics);
        }

        [Fact]
        public void OpenEnvironment_Deserialize_WithBareOptions_FailsWithoutConverters()
        {
            // Bare JsonSerializerOptions (no custom converters) cannot handle the
            // generated model's [JsonConstructor] with Option<T> parameters.
            // This proves the custom converters from HostConfiguration are essential.
            var json = @"{""id"":""e095ed11-8ff4-49c4-beee-7369ae99d14d""}";
            var options = new JsonSerializerOptions();

            // Act & Assert: fails because reflection-based deserialization can't bind
            // the Option<EnvironmentDiagnostics?> constructor parameter.
            Assert.Throws<InvalidOperationException>(() =>
                JsonSerializer.Deserialize<OpenEnvironment>(json, options));
        }

        #endregion

        #region Bug 2: EnvironmentDiagnostics deserialization

        [Fact]
        public void EnvironmentDiagnostics_Deserialize_WithHostConfigOptions_Succeeds()
        {
            // Arrange: JSON returned after UpdateEnvironmentYaml
            var json = @"{""diagnostics"":[]}";
            var options = CreateHostConfigurationOptions();

            // Act
            var result = JsonSerializer.Deserialize<EnvironmentDiagnostics>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result!.Diagnostics);
            Assert.Empty(result.Diagnostics!);
        }

        [Fact]
        public void EnvironmentDiagnostics_Deserialize_WithWarning_Succeeds()
        {
            // Pos requires byte, column, line; Range requires environment, begin, end
            var json = @"{""diagnostics"":[{""range"":{""environment"":""test"",""begin"":{""byte"":0,""line"":1,""column"":0},""end"":{""byte"":5,""line"":1,""column"":5}},""summary"":""test warning""}]}";
            var options = CreateHostConfigurationOptions();

            // Act
            var result = JsonSerializer.Deserialize<EnvironmentDiagnostics>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result!.Diagnostics!);
            Assert.Equal("test warning", result.Diagnostics![0].Summary);
        }

        [Fact]
        public void EnvironmentDiagnostics_Deserialize_WithBareOptions_FailsWithoutConverters()
        {
            // Bare options can't handle generated models with Option<T> constructor parameters.
            var json = @"{""diagnostics"":[]}";
            var options = new JsonSerializerOptions();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                JsonSerializer.Deserialize<EnvironmentDiagnostics>(json, options));
        }

        #endregion

        #region Bug 3: YAML string JSON serialization (generated code issue)

        [Fact]
        public void YamlString_JsonSerialize_WrapsInQuotes()
        {
            // This demonstrates Bug 3: when the generated code does
            // JsonSerializer.Serialize(yamlString, options), the YAML gets
            // wrapped in JSON string quotes, corrupting the API request body.
            var yaml = "values:\n  foo: bar\n";
            var options = CreateHostConfigurationOptions();

            var serialized = JsonSerializer.Serialize(yaml, options);

            // The serialized result is a JSON string literal (with quotes and escapes)
            // NOT the raw YAML. This proves the generated code would send wrong content.
            Assert.StartsWith("\"", serialized);
            Assert.Contains("\\n", serialized);
            Assert.NotEqual(yaml, serialized);
        }

        #endregion

        #region Runtime info (diagnostic test)

        [Fact]
        public void RuntimeInfo_ReportVersion()
        {
            var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            var stjVersion = typeof(JsonSerializer).Assembly.GetName().Version;

            // This test always passes — it just logs runtime info for debugging
            Assert.NotNull(runtime);
            Assert.NotNull(stjVersion);

            // Output for test runner (visible with -v d or in test output)
            Console.WriteLine($"Runtime: {runtime}");
            Console.WriteLine($"System.Text.Json version: {stjVersion}");

            // Check if TypeInfoResolver property exists (added in .NET 7 / STJ 7.0)
            var prop = typeof(JsonSerializerOptions).GetProperty("TypeInfoResolver");
            Console.WriteLine($"TypeInfoResolver property exists: {prop != null}");
        }

        #endregion
    }
}
