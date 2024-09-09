// Copyright 2016-2024, Pulumi Corporation

using Grpc.Net.Client;
using Pulumi.Experimental.Provider;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Pulumi.Tests.Provider
{
    public class ConfigureProviderServerTest : IClassFixture<ProviderServerTestHost<ConfigureProviderServerTest.TestConfigureProvider>>
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

        public ConfigureProviderServerTest(ProviderServerTestHost<TestConfigureProvider> testHost)
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
                provider.GetSchema(new GetSchemaRequest(0), CancellationToken.None));
            Assert.Contains("GetSchema", exc.Message);
        }

    }

    public class ProviderServerTest
    {
        sealed class TestLoggingProvider : Experimental.Provider.Provider
        {
            readonly IHost Host;

            public TestLoggingProvider(IHost host)
            {
                Host = host;
            }

            public override async Task<ConfigureResponse> Configure(ConfigureRequest request, CancellationToken ct)
            {
                await Host.LogAsync(new LogMessage(LogSeverity.Info, "Configure called"));

                return new ConfigureResponse()
                {
                    AcceptSecrets = true,
                };
            }
        }

        sealed class TestHost : IHost
        {
            public List<LogMessage> LogMessages { get; } = new List<LogMessage>();
            public Task LogAsync(LogMessage message)
            {
                LogMessages.Add(message);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task CheckLoggingCanBeCalled()
        {
            var ihost = new TestHost();
            IHost BuildHost(string address)
            {
                Assert.Equal("127.0.0.1:9999", address);
                return ihost;
            }

            var args = new[] { "127.0.0.1:9999" };
            var cts = new System.Threading.CancellationTokenSource();
            var host = Experimental.Provider.Provider.BuildHost(args, "1.0", host => new TestLoggingProvider(host), BuildHost);
            await host.StartAsync(cts.Token);
            var hostUri = Experimental.Provider.Provider.GetHostUri(host);
            var channel = GrpcChannel.ForAddress(hostUri, new GrpcChannelOptions
            {
                Credentials = Grpc.Core.ChannelCredentials.Insecure,
            });
            var client = new Pulumirpc.ResourceProvider.ResourceProviderClient(channel);

            // Call configure and then check we got the expected log message
            var configureResult = await client.ConfigureAsync(new Pulumirpc.ConfigureRequest() { });
            Assert.True(configureResult.AcceptSecrets);
            var message = Assert.Single(ihost.LogMessages);
            Assert.Equal(LogSeverity.Info, message.Severity);
            Assert.Equal("Configure called", message.Message);
        }
    }
}
