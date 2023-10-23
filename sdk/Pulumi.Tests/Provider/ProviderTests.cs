// Copyright 2016-2023, Pulumi Corporation

using Grpc.Net.Client;
using Pulumi.Experimental.Provider;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

            // Custom stdout so we can see what port Serve chooses
            var stdout = new System.IO.StringWriter();
            var server = Pulumi.Experimental.Provider.Provider.Serve(args, "1.0", _ => new TestConfigureProvider(), cts.Token, stdout);

            // Grab the port from stdout and create a connection to it
            var port = int.Parse(stdout.ToString().Trim());

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

        [Fact]
        public async Task NotImplementedErrorIncludesName()
        {
            var provider = new TestConfigureProvider();
            var exc = await Assert.ThrowsAsync<NotImplementedException>(() =>
                provider.GetSchema(new GetSchemaRequest(0), CancellationToken.None));
            Assert.Contains("GetSchema", exc.Message);
        }

        class BasicArgs : ResourceArgs
        {
            public int PasswordLength { get; set; }
        }

        async Task<T> DeserializeObject<T>(Dictionary<string, object?> inputs)
        {
            var propertyValue = await PropertyValue.From(inputs);
            if (propertyValue.TryGetObject(out var objectInputs) && objectInputs != null)
            {
                var deserialized = PropertyValue.DeserializeObject<T>(objectInputs);
                return deserialized;
            }

            throw new Exception("Expected object");
        }

        [Fact]
        public async Task DeserializingBasicArgsWorks()
        {
            var basicArgs = await DeserializeObject<BasicArgs>(new Dictionary<string, object?>
            {
                ["PasswordLength"] = 10
            });

            Assert.Equal(10, basicArgs.PasswordLength);
        }

        class UsingNullableArgs : ResourceArgs
        {
            public int? Length { get; set; }
        }

        [Fact]
        public async Task DeserializingNullableArgsWorks()
        {
            var emptyData = new Dictionary<string, object?>();
            var withoutData = await DeserializeObject<UsingNullableArgs>(emptyData);
            Assert.False(withoutData.Length.HasValue, "Nullable value is null");

            var withData = await DeserializeObject<UsingNullableArgs>(new Dictionary<string, object?>
            {
                ["Length"] = 10
            });

            Assert.True(withData.Length.HasValue, "Nullable field has a value");
            Assert.Equal(10, withData.Length.Value);
        }

        class UsingListArgs : ResourceArgs
        {
            public string[] First { get; set; }
            public List<string> Second { get; set; }
            public ImmutableArray<string> Third { get; set; }
        }

        [Fact]
        public async Task DeserializingListTypesWorks()
        {
            var data = new [] { "one", "two", "three" };
            var args = await DeserializeObject<UsingListArgs>(new Dictionary<string, object?>
            {
                ["First"] = data,
                ["Second"] = data,
                ["Third"] = data
            });

            Assert.Equal(data, args.First);
            Assert.Equal(data, args.Second.ToArray());
            Assert.Equal(data, args.Third.ToArray());

            var withEmptyArgs = await DeserializeObject<UsingListArgs>(new Dictionary<string, object?>());

            Assert.Equal(0, withEmptyArgs.First.Length);
            Assert.Equal(0, withEmptyArgs.Second.Count);
            Assert.Equal(0, withEmptyArgs.Third.Length);
        }

        class StringFromNullBecomesEmpty : ResourceArgs
        {
            public string Data { get; set; }
        }

        [Fact]
        public async Task DeserializingStringFromNullValueMakesItEmptyString()
        {
            var argsWithNullString = await DeserializeObject<StringFromNullBecomesEmpty>(new Dictionary<string, object?>
            {
                ["Data"] = null
            });

            Assert.Equal("", argsWithNullString.Data);
        }

        class UsingDictionaryArgs : ResourceArgs
        {
            public Dictionary<string, string> First { get; set; }
            public ImmutableDictionary<string, string> Second { get; set; }
        }

        [Fact]
        public async Task DeserializingDictionaryPropertiesWork()
        {
            var data = new Dictionary<string, string>
            {
                ["Uno"] = "One"
            };

            var args = await DeserializeObject<UsingDictionaryArgs>(new Dictionary<string, object?>
            {
                ["First"] = data,
                ["Second"] = data
            });

            Assert.Equal(data, args.First);
            Assert.Equal(data, args.Second.ToDictionary(x => x.Key, y => y.Value));

            var emptyArgs = await DeserializeObject<UsingDictionaryArgs>(new Dictionary<string, object?>());

            Assert.Equal(0, emptyArgs.First.Count());
            Assert.Equal(0, emptyArgs.Second.Count());
        }

        class UsingInputArgs : ResourceArgs
        {
            public Input<string> Name { get; set; }
            public InputList<string> Subnets { get; set; }
            public InputMap<string> Tags { get; set; }
        }

        [Fact]
        public async Task DeserializingInputTypesWorks()
        {
            var args = await DeserializeObject<UsingInputArgs>(new Dictionary<string, object?>
            {
                ["Name"] = "test",
                ["Subnets"] = new [] { "one", "two", "three" },
                ["Tags"] = new Dictionary<string, object?>
                {
                    ["one"] = "one",
                    ["two"] = "two",
                    ["three"] = "three"
                }
            });

            var name = await args.Name.ToOutput().GetValueAsync("");
            Assert.Equal("test", name);

            var subnets = await args.Subnets.ToOutput().GetValueAsync(ImmutableArray<string>.Empty);
            Assert.Equal(new [] { "one", "two", "three" }, subnets.ToArray());

            var tags = await args.Tags.ToOutput().GetValueAsync(ImmutableDictionary<string, string>.Empty);
            Assert.Equal(new Dictionary<string, string>
            {
                ["one"] = "one",
                ["two"] = "two",
                ["three"] = "three"
            }, tags);
        }

        [Fact]
        public async Task DeserializingInputsFromEmptyValuesWorks()
        {
            var args = await DeserializeObject<UsingInputArgs>(new Dictionary<string, object?>());

            var name = await args.Name.ToOutput().GetValueAsync("<unknown>");
            Assert.Equal("", name);

            var unknownDefaultSubnets = (new List<string> { "<unknown>" }).ToImmutableArray();
            var subnets = await args.Subnets.ToOutput().GetValueAsync(unknownDefaultSubnets);
            Assert.Equal(new string[] { }, subnets.ToArray());

            var unknownDefaultTags =
                (new Dictionary<string, string> { ["tag"] = "deafult" })
                .ToImmutableDictionary();

            var tags = await args.Tags.ToOutput().GetValueAsync(unknownDefaultTags);
            Assert.Equal(0, tags.Count);
        }

        class RequireIntInputArgs : ResourceArgs
        {
            public Input<int> Property { get; set; }
        }

        [Fact]
        public async Task DeserializingEmptyValuesIntoRequiredIntegerShouldFail()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var args =
                    DeserializeObject<RequireIntInputArgs>(new Dictionary<string, object?>())
                        .GetAwaiter()
                        .GetResult();
            });
        }

        class OptionalIntInputArgs : ResourceArgs
        {
            public Input<int?> OptionalInteger { get; set; }
        }

        [Fact]
        public async Task DeserializingOptionalInputWorks()
        {
            var args = await DeserializeObject<OptionalIntInputArgs>(new Dictionary<string, object?>());
            var optionalInteger = await args.OptionalInteger.ToOutput().GetValueAsync(0);
            Assert.False(optionalInteger.HasValue);
        }
    }
}
