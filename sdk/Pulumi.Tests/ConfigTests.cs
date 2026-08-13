// Copyright 2016-2026, Pulumi Corporation

using System.Threading.Tasks;
using Moq;
using Pulumi.Utilities;
using Xunit;

namespace Pulumi.Tests
{
    /// <summary>
    /// Regression tests covering <see cref="Config"/> deserialization of structured configuration
    /// values into custom .NET types (see
    /// https://github.com/pulumi/pulumi-dotnet/issues/17).
    /// </summary>
    public class ConfigTests
    {
        private const string ConfigName = "testproject";

        private class DatabaseCredentials
        {
            public string? Host { get; set; }
            public int Port { get; set; }
        }

        private static Config CreateConfig(string key, string? value, bool isSecret = false)
        {
            var runner = new Mock<IRunner>(MockBehavior.Strict);
            runner.Setup(r => r.RegisterTask(It.IsAny<string>(), It.IsAny<Task>()));

            var mock = new Mock<IDeploymentInternal>(MockBehavior.Strict);
            mock.Setup(d => d.GetConfig($"{ConfigName}:{key}")).Returns(value);
            mock.Setup(d => d.IsConfigSecret($"{ConfigName}:{key}")).Returns(isSecret);
            mock.Setup(d => d.Runner).Returns(runner.Object);

            Deployment.Instance = new DeploymentInstance(mock.Object);

            return new Config(ConfigName);
        }

        // Note: System.Text.Json's default settings are case-sensitive, so JSON property names
        // must match the C# property names exactly (e.g. via [JsonPropertyName]) unless custom
        // JsonSerializerOptions are supplied. See https://github.com/pulumi/pulumi-dotnet/issues/370.
        [Fact]
        public void GetObject_DeserializesJsonIntoCustomType()
        {
            var config = CreateConfig("databaseCredentials", "{\"Host\":\"localhost\",\"Port\":5432}");

            var result = config.GetObject<DatabaseCredentials>("databaseCredentials");

            Assert.NotNull(result);
            Assert.Equal("localhost", result!.Host);
            Assert.Equal(5432, result.Port);
        }

        [Fact]
        public void GetObject_ReturnsNullWhenKeyIsMissing()
        {
            var config = CreateConfig("databaseCredentials", null);

            var result = config.GetObject<DatabaseCredentials>("databaseCredentials");

            Assert.Null(result);
        }

        [Fact]
        public void RequireObject_DeserializesJsonIntoCustomType()
        {
            var config = CreateConfig("databaseCredentials", "{\"Host\":\"db.example.com\",\"Port\":1234}");

            var result = config.RequireObject<DatabaseCredentials>("databaseCredentials");

            Assert.Equal("db.example.com", result.Host);
            Assert.Equal(1234, result.Port);
        }

        [Fact]
        public void RequireObject_ThrowsWhenKeyIsMissing()
        {
            var config = CreateConfig("databaseCredentials", null);

            Assert.ThrowsAny<RunException>(() => config.RequireObject<DatabaseCredentials>("databaseCredentials"));
        }

        [Fact]
        public void RequireObject_ThrowsOnInvalidJson()
        {
            var config = CreateConfig("databaseCredentials", "not-json");

            Assert.ThrowsAny<RunException>(() => config.RequireObject<DatabaseCredentials>("databaseCredentials"));
        }

        [Fact]
        public async Task GetSecretObject_DeserializesAndMarksValueAsSecret()
        {
            var config = CreateConfig("databaseCredentials", "{\"Host\":\"localhost\",\"Port\":5432}", isSecret: true);

            var output = config.GetSecretObject<DatabaseCredentials>("databaseCredentials");

            Assert.NotNull(output);
            var value = await OutputUtilities.GetValueAsync(output!);
            Assert.Equal("localhost", value.Host);
            Assert.True(await Output.IsSecretAsync(output!));
        }

        [Fact]
        public void GetSecretObject_ReturnsNullWhenKeyIsMissing()
        {
            var config = CreateConfig("databaseCredentials", null);

            var output = config.GetSecretObject<DatabaseCredentials>("databaseCredentials");

            Assert.Null(output);
        }

        [Fact]
        public async Task RequireSecretObject_DeserializesAndMarksValueAsSecret()
        {
            var config = CreateConfig("databaseCredentials", "{\"Host\":\"localhost\",\"Port\":5432}", isSecret: true);

            var output = config.RequireSecretObject<DatabaseCredentials>("databaseCredentials");

            var value = await OutputUtilities.GetValueAsync(output);
            Assert.Equal("localhost", value.Host);
            Assert.True(await Output.IsSecretAsync(output));
        }

        [Fact]
        public void RequireSecretObject_ThrowsWhenKeyIsMissing()
        {
            var config = CreateConfig("databaseCredentials", null);

            Assert.ThrowsAny<RunException>(() => config.RequireSecretObject<DatabaseCredentials>("databaseCredentials"));
        }
    }
}
