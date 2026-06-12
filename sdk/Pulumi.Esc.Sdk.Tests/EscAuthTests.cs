// Copyright 2024, Pulumi Corporation.  All rights reserved.

using System;
using System.IO;
using Xunit;

namespace Pulumi.Esc.Sdk.Tests
{
    /// <summary>
    /// Unit tests for <see cref="EscAuth"/> credential resolution.
    /// Uses the shared test fixtures in sdk/test/ (same as Python and TypeScript tests).
    /// </summary>
    public class EscAuthTests : IDisposable
    {
        private readonly string? _tokenBefore;
        private readonly string? _backendBefore;
        private readonly string? _homeBefore;

        public EscAuthTests()
        {
            _tokenBefore = Environment.GetEnvironmentVariable("PULUMI_ACCESS_TOKEN");
            _backendBefore = Environment.GetEnvironmentVariable("PULUMI_BACKEND_URL");
            _homeBefore = Environment.GetEnvironmentVariable("PULUMI_HOME");

            // Clear env vars so credential file logic is exercised
            Environment.SetEnvironmentVariable("PULUMI_ACCESS_TOKEN", "");
            Environment.SetEnvironmentVariable("PULUMI_BACKEND_URL", "");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("PULUMI_ACCESS_TOKEN", _tokenBefore ?? "");
            Environment.SetEnvironmentVariable("PULUMI_BACKEND_URL", _backendBefore ?? "");
            Environment.SetEnvironmentVariable("PULUMI_HOME", _homeBefore ?? "");
        }

        /// <summary>
        /// Returns the path to the shared test fixtures directory (sdk/test/).
        /// </summary>
        private static string GetTestFixturesDir()
        {
            // The test runs from sdk/csharp/Pulumi.Esc.Sdk.Tests/bin/Debug/net6.0/
            // Go up 5 levels to reach sdk/, then into test/
            var assemblyDir = AppContext.BaseDirectory;
            var sdkDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
            var testDir = Path.GetFullPath(Path.Combine(sdkDir, "test"));
            return testDir;
        }

        [Fact]
        public void NoCreds_ThrowsAndDefaultsUrl()
        {
            Environment.SetEnvironmentVariable("PULUMI_HOME", "/not_real_dir");

            Assert.Throws<InvalidOperationException>(() => EscAuth.GetDefaultAccessToken());
            Assert.Equal("https://api.pulumi.com", EscAuth.GetDefaultBackendUrl());
        }

        [Fact]
        public void JustPulumiCreds_ReadsCurrentAccount()
        {
            var fixturesDir = GetTestFixturesDir();
            Environment.SetEnvironmentVariable("PULUMI_HOME", Path.Combine(fixturesDir, "test_pulumi_home"));

            var token = EscAuth.GetDefaultAccessToken();
            var backendUrl = EscAuth.GetDefaultBackendUrl();

            Assert.Equal("pul-fake-token-moo", token);
            Assert.Equal("https://api.moolumi.com", backendUrl);
        }

        [Fact]
        public void PulumiCredsWithEsc_EscOverridesCurrent()
        {
            var fixturesDir = GetTestFixturesDir();
            Environment.SetEnvironmentVariable("PULUMI_HOME", Path.Combine(fixturesDir, "test_pulumi_home_esc"));

            var token = EscAuth.GetDefaultAccessToken();
            var backendUrl = EscAuth.GetDefaultBackendUrl();

            // ESC credentials set "name": "https://api.boolumi.com" which overrides
            // the Pulumi CLI "current": "https://api.moolumi.com"
            Assert.Equal("pul-fake-token-boo", token);
            Assert.Equal("https://api.boolumi.com", backendUrl);
        }

        [Fact]
        public void BadFormatCreds_FallsBackToAccessTokens()
        {
            var fixturesDir = GetTestFixturesDir();
            Environment.SetEnvironmentVariable("PULUMI_HOME", Path.Combine(fixturesDir, "test_pulumi_home_bad_format"));

            // The bad_format fixture has "accounts" with a non-matching key ("bad edit"),
            // but "accessTokens" still maps valid URLs to tokens. Our C# implementation
            // falls back to accessTokens when accounts doesn't match, so a token IS found.
            // The ESC credentials file has "notName" instead of "name", so it's ignored.
            var token = EscAuth.GetDefaultAccessToken();
            Assert.Equal("pul-fake-token-moo", token);

            var backendUrl = EscAuth.GetDefaultBackendUrl();
            Assert.Equal("https://api.moolumi.com", backendUrl);
        }

        [Fact]
        public void EnvVarOverridesFiles()
        {
            var fixturesDir = GetTestFixturesDir();
            Environment.SetEnvironmentVariable("PULUMI_HOME", Path.Combine(fixturesDir, "test_pulumi_home"));

            // Set env vars — they should take priority over credential files
            Environment.SetEnvironmentVariable("PULUMI_ACCESS_TOKEN", "env-token-123");
            Environment.SetEnvironmentVariable("PULUMI_BACKEND_URL", "https://custom.backend.com");

            var token = EscAuth.GetDefaultAccessToken();
            var backendUrl = EscAuth.GetDefaultBackendUrl();

            Assert.Equal("env-token-123", token);
            Assert.Equal("https://custom.backend.com", backendUrl);
        }

        [Fact]
        public void GetEscApiUrl_ConvertsBackendUrl()
        {
            Assert.Equal("https://api.pulumi.com/api/esc", EscAuth.GetEscApiUrl("https://api.pulumi.com"));
            Assert.Equal("https://api.pulumi.com/api/esc", EscAuth.GetEscApiUrl("https://api.pulumi.com/"));
            Assert.Equal("https://custom.example.com/api/esc", EscAuth.GetEscApiUrl("https://custom.example.com"));
            Assert.Equal("http://localhost:8080/api/esc", EscAuth.GetEscApiUrl("http://localhost:8080"));
        }
    }
}
