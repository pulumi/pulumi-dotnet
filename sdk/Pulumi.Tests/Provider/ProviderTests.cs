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
    public class ProviderServerTest
    {
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

        [Fact]
        public async Task StateIsPersistent()
        {
            // Test that if we serve the TestConfigureProvider and configure it, that internal state is preseved for later calls

            // We're not going to call anything on the host so we can just have an empty tcp port to listen on
            var host = System.Net.Sockets.TcpListener.Create(0);
            var args = new string[] { host.LocalEndpoint.ToString() };

            var cts = new System.Threading.CancellationTokenSource();

            // Hook stdout so we can see what port Serve chooses
            var stringWriter = new System.IO.StringWriter();
            System.Console.SetOut(stringWriter);

            var server = Pulumi.Experimental.Provider.Provider.Serve(args, "1.0", _ => new TestConfigureProvider(), cts.Token);

            // Grab the port from stdout and create a connection to it
            var port = int.Parse(stringWriter.ToString().Trim());


            // Inititialize the engine channel once for this address
            var channel = GrpcChannel.ForAddress(new Uri($"http://localhost:{port}"), new GrpcChannelOptions
            {
                Credentials = Grpc.Core.ChannelCredentials.Insecure,
            });
            var provider = new Pulumirpc.ResourceProvider.ResourceProviderClient(channel);

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
    }
}
