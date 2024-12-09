using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Pulumi.Tests
{
    public class ConfigTests
    {
        private Config target = new Config("cfg");
        private Mock<IDeploymentInternal> deploymentInternal = new Mock<IDeploymentInternal>();
        private const string PascalCaseJson = "{ \"Value\":\"Value\" }";
        private const string CamelCaseJson = "{ \"value\":\"Value\" }";
        private static readonly JsonSerializerOptions CamelCaseSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public ConfigTests()
        {
            var runner = new Mock<IRunner>();
            deploymentInternal.Setup(x => x.Runner).Returns(runner.Object);
            Deployment.Instance = new DeploymentInstance(deploymentInternal.Object);
        }


        private class TestConfig
        {
            public string? Value { get; set; }
        }

        [Fact]
        public void GetObjectReturnsObject()
        {
            deploymentInternal.Setup(x => x.GetConfig("cfg:test")).Returns(PascalCaseJson);

            var result = target.GetObject<TestConfig>("test");

            Assert.NotNull(result);
            Assert.Equal("Value", result?.Value);
        }

        [Fact]
        public void GetObjectReturnsObjectWithCustomJsonSerializerOptions()
        {
            deploymentInternal.Setup(x => x.GetConfig("cfg:test")).Returns(CamelCaseJson);

            var result = target.GetObject<TestConfig>("test", CamelCaseSerializerOptions);

            Assert.NotNull(result);
            Assert.Equal("Value", result?.Value);
        }

        [Fact]
        public async Task GetSecretObjectReturnsObject()
        {
            deploymentInternal.Setup(x => x.GetConfig("cfg:test")).Returns(PascalCaseJson);

            var output = target.GetSecretObject<TestConfig>("test");
            var result = await output.GetValueAsync(new TestConfig());

            Assert.Equal("Value", result.Value);
        }

        [Fact]
        public async Task GetSecretObjectReturnsObjectWithCustomJsonSerializerOptions()
        {
            deploymentInternal.Setup(x => x.GetConfig("cfg:test")).Returns(CamelCaseJson);

            var output = target.GetSecretObject<TestConfig>("test", CamelCaseSerializerOptions);
            var result = await output.GetValueAsync(new TestConfig());

            Assert.Equal("Value", result.Value);
        }

        [Fact]
        public void RequireObjectReturnsObject()
        {
            deploymentInternal.Setup(x => x.GetConfig("cfg:test")).Returns(PascalCaseJson);

            var result = target.RequireObject<TestConfig>("test");

            Assert.NotNull(result);
            Assert.Equal("Value", result?.Value);
        }

        [Fact]
        public void RequireObjectReturnsObjectWithCustomJsonSerializerOptions()
        {
            deploymentInternal.Setup(x => x.GetConfig("cfg:test")).Returns(CamelCaseJson);

            var result = target.RequireObject<TestConfig>("test", CamelCaseSerializerOptions);

            Assert.NotNull(result);
            Assert.Equal("Value", result?.Value);
        }

        [Fact]
        public async Task RequireSecretObjectReturnsObject()
        {
            deploymentInternal.Setup(x => x.GetConfig("cfg:test")).Returns(PascalCaseJson);

            var output = target.RequireSecretObject<TestConfig>("test");
            var result = await output.GetValueAsync(new TestConfig());

            Assert.Equal("Value", result.Value);
        }

        [Fact]
        public async Task RequireSecretObjectReturnsObjectWithCustomJsonSerializerOptions()
        {
            deploymentInternal.Setup(x => x.GetConfig("cfg:test")).Returns(CamelCaseJson);

            var output = target.RequireSecretObject<TestConfig>("test", CamelCaseSerializerOptions);
            var result = await output.GetValueAsync(new TestConfig());

            Assert.Equal("Value", result.Value);
        }
    }
}
