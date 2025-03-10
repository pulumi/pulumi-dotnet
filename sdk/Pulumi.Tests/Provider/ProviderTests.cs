// Copyright 2016-2023, Pulumi Corporation

using Grpc.Net.Client;
using Pulumi.Experimental.Provider;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Pulumi.Tests.Provider
{
    public class ProviderServerTest : IClassFixture<ProviderServerTestHost<ProviderServerTest.TestConfigureProvider>>
    {
        private readonly ProviderServerTestHost<TestConfigureProvider> testHost;

        public sealed class TestConfigureProvider : Pulumi.Experimental.Provider.Provider
        {
            private string configString = "default";

            public override Task<ConfigureResponse> Configure(ConfigureRequest request, CancellationToken ct)
            {
                if (!request.Args.TryGetValue("test", out var testValue))
                {
                    throw new Exception("Expected test key");
                }

                if (!testValue.TryGetString(out var testString))
                {
                    throw new Exception("Expected test key to be a string");
                }

                this.configString = testString;

                return Task.FromResult(new ConfigureResponse()
                {
                    AcceptSecrets = true,
                });
            }

            public override Task<CheckResponse> Check(CheckRequest request, CancellationToken ct)
            {
                return Task.FromResult(new CheckResponse()
                {
                    Inputs = new Dictionary<string, PropertyValue>()
                    {
                        { "output", new PropertyValue(this.configString) }
                    }
                });
            }
        }

        public ProviderServerTest(ProviderServerTestHost<TestConfigureProvider> testHost)
        {
            this.testHost = testHost;
        }

        [Fact]
        public async Task StateIsPersistent()
        {
            // Test that if we serve the TestConfigureProvider and configure it, that internal state is preseved for later calls

            // We're not going to call anything on the host so we can just have an empty tcp port to listen on
            var provider = new Pulumirpc.ResourceProvider.ResourceProviderClient(testHost.Channel);

            var configureArgs = new Google.Protobuf.WellKnownTypes.Struct();
            configureArgs.Fields.Add("test", Google.Protobuf.WellKnownTypes.Value.ForString("testing"));
            var configureResult = await provider.ConfigureAsync(new Pulumirpc.ConfigureRequest() { Args = configureArgs });

            Assert.True(configureResult.AcceptSecrets);

            // Now call check and make sure it returns the internal state
            var checkResult = await provider.CheckAsync(new Pulumirpc.CheckRequest());

            Assert.True(checkResult.Inputs.Fields.TryGetValue("output", out var outputValue));
            Assert.True(outputValue.KindCase == Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StringValue);
            Assert.Equal("testing", outputValue.StringValue);
        }

        [Fact]
        public async Task NotImplementedErrorIncludesName()
        {
            var provider = new TestConfigureProvider();
            var exc = await Assert.ThrowsAsync<NotImplementedException>(() =>
                provider.GetSchema(new GetSchemaRequest(0, null, null), CancellationToken.None));
            Assert.Contains("GetSchema", exc.Message);
        }
    }

    public class ProviderEngineAddressTests
    {
        private static string? GetEngineAddress(string[] args)
        {
            // Helper method to access the private static method for testing
            var method = typeof(Pulumi.Experimental.Provider.Provider).GetMethod(
                "GetEngineAddress", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (string?)method?.Invoke(null, new object[] { args });
        }

        [Fact]
        public void GetEngineAddress_WithValidAddress_ReturnsAddress()
        {
            var args = new[] { "127.0.0.1:51776" };
            var address = GetEngineAddress(args);
            Assert.Equal("127.0.0.1:51776", address);
        }

        [Fact]
        public void GetEngineAddress_WithLoggingArgs_ReturnsAddress()
        {
            var args = new[] { 
                "--logtostderr",
                "-v=3",
                "127.0.0.1:51776",
                "--logflow"
            };
            var address = GetEngineAddress(args);
            Assert.Equal("127.0.0.1:51776", address);
        }

        [Fact]
        public void GetEngineAddress_WithTracingArg_ReturnsAddress()
        {
            var args = new[] {
                "--tracing",
                "1",
                "127.0.0.1:51776"
            };
            var address = GetEngineAddress(args);
            Assert.Equal("127.0.0.1:51776", address);
        }

        [Fact]
        public void GetEngineAddress_WithNoArgs_ThrowsException()
        {
            var args = Array.Empty<string>();
            var ex = Assert.Throws<ArgumentException>(() => GetEngineAddress(args));
            Assert.Equal("No engine address provided in arguments", ex.Message);
        }

        [Fact]
        public void GetEngineAddress_WithOnlyLoggingArgs_ThrowsException()
        {
            var args = new[] { "--logtostderr", "-v=3", "--logflow" };
            var ex = Assert.Throws<ArgumentException>(() => GetEngineAddress(args));
            Assert.Equal("No engine address provided in arguments", ex.Message);
        }

        [Fact]
        public void GetEngineAddress_WithMultipleAddresses_ThrowsException()
        {
            var args = new[] {
                "127.0.0.1:51776",
                "127.0.0.1:51777"
            };
            var ex = Assert.Throws<ArgumentException>(() => GetEngineAddress(args));
            Assert.Equal(
                "Expected exactly one engine address argument, but got 2 non-logging arguments",
                ex.Message);
        }
    }
}
