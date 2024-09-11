using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Experimental.Provider;
using Pulumi.Utilities;
using Xunit;
using Xunit.Abstractions;
using CallRequest = Pulumirpc.CallRequest;

namespace Pulumi.Tests.Provider;

public class ResourceProviderServiceTest : IClassFixture<ProviderServerTestHost<ResourceProviderServiceTest.ResourceProviderServiceTestProvider>>
{
    private readonly ProviderServerTestHost<ResourceProviderServiceTestProvider> testHost;

    public ResourceProviderServiceTest(ProviderServerTestHost<ResourceProviderServiceTestProvider> testHost, ITestOutputHelper testOutputHelper)
    {
        this.testHost = testHost;
        this.testHost.LoggerProvider = new TestOutputLoggerProvider(testOutputHelper);
    }

    [Theory]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 3)]
    public async Task Construct(bool dryRun, bool retainOnDelete, int parallel)
    {
        // Test that if we serve the TestConfigureProvider and configure it, that internal state is preseved for later calls

        // We're not going to call anything on the host so we can just have an empty tcp port to listen on
        var provider = new Pulumirpc.ResourceProvider.ResourceProviderClient(testHost.Channel);

        var stringInput = "Hello World";
        var args = new TestBucketArgs()
        {
            StringInput = stringInput
        };

        var serializeArga = await Serialize(args);

        var stringInputName = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(nameof(args.StringInput));
        var urnDependentResource = "urn::dependent::resource";
        var constructRequest = new Pulumirpc.ConstructRequest()
        {
            Inputs = serializeArga.StructValue,
            InputDependencies =
            {
                {
                    stringInputName,
                    new Pulumirpc.ConstructRequest.Types.PropertyDependencies() { Urns = { urnDependentResource } }
                }
            },
            Name = "SomeName",
            Type = ResourceProviderServiceTestProvider.BucketType,
            Organization = "SomeOrganization",
            Project = "SomeProject",
            Stack = "SomeStack",
            MonitorEndpoint = "https://dummy.test.host",
            Config = { { "key", "value" }, { "secretKey", "secretValue" } },
            ConfigSecretKeys = { "secretKey" },
            CustomTimeouts = (new CustomTimeouts()
            {
                Create = TimeSpan.FromSeconds(10),
                Update = TimeSpan.FromSeconds(20),
                Delete = TimeSpan.FromSeconds(30)
            }).ForConstructRequest(),
            DryRun = dryRun,
            RetainOnDelete = retainOnDelete,
            Parallel = parallel,
        };

        constructRequest.Parent = $"urn:pulumi:{constructRequest.Stack}::{constructRequest.Project}::ParentType::parentResource";

        var constructResponse = await provider.ConstructAsync(constructRequest);

        Assert.Equal(constructResponse.Urn, $"urn:pulumi:{constructRequest.Stack}::{constructRequest.Project}::ParentType${nameof(TestBucket)}::{constructRequest.Name}");
        constructResponse.State.Fields.Should().Contain(kv => kv.Key == nameof(TestBucket.TestBucketOutput) && kv.Value.StringValue == stringInput);
    }

    private static async Task<Value> Serialize(object args)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var propertyValueSerializer = new PropertyValueSerializer();
#pragma warning restore CS0618 // Type or member is obsolete
        var marshalled = PropertyValue.Marshal(await propertyValueSerializer.Serialize(args));

        return marshalled;
    }


    [Theory]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 3)]
    public async Task Call(bool dryRun, bool acceptsOutputValues, int parallel)
    {
        // Test that if we serve the TestConfigureProvider and configure it, that internal state is preseved for later calls

        // We're not going to call anything on the host so we can just have an empty tcp port to listen on
        var provider = new Pulumirpc.ResourceProvider.ResourceProviderClient(testHost.Channel);

        var args = new Dictionary<string, object?>()
        {
            { "argString", "Hello World" },
            { "argInt", 10 },
            { "argDictionary", new Dictionary<string, object?> { { "nested", "dictionary" } } }
        };

        var serializeArgs = await Serialize(args);

        var callRequest = new CallRequest()
        {
            Organization = "SomeOrganization",
            Project = "SomeProject",
            Stack = "SomeStack",
            MonitorEndpoint = "https://dummy.test.host",
            Config = { { "key", "value" }, { "secretKey", "secretValue" } },
            ConfigSecretKeys = { "secretKey" },
            DryRun = dryRun,
            Parallel = parallel,
            Tok = "SomeMethod",
            Args = serializeArgs.StructValue,
            ArgDependencies = { },
            AcceptsOutputValues = acceptsOutputValues
        };

        var callResponse = await provider.CallAsync(callRequest);
        Assert.True(callResponse.Return.Fields.TryGetValue(ResourceProviderServiceTestProvider.Tok, out var tokResponse));
        Assert.Equal(tokResponse!.StringValue, callRequest.Tok);
        Assert.True(callResponse.Return.Fields.TryGetValue(ResourceProviderServiceTestProvider.Args, out var argsResponse));
        argsResponse.Should().BeEquivalentTo(serializeArgs);
        callResponse.Failures.Should().ContainSingle(failure =>
            failure.Property == ResourceProviderServiceTestProvider.CheckFailure.Property &&
            failure.Reason == ResourceProviderServiceTestProvider.CheckFailure.Reason);
        callResponse.ReturnDependencies.Should().Contain(kv => kv.Key == ResourceProviderServiceTestProvider.DependentFieldName).Subject.Value.Urns.Should()
            .BeEquivalentTo(ResourceProviderServiceTestProvider.DependentUrns.Select(urn => urn.Value));
    }

    public class TestBucketArgs : ResourceArgs
    {
        [Input("stringInput", required: true)]
        public Input<string> StringInput { get; set; } = default!;
    }

    public class TestBucket : ComponentResource
    {
        [Output]
        public Output<string> TestBucketOutput { get; private set; }

        public TestBucket(string name, TestBucketArgs args, ComponentResourceOptions options)
            : base(nameof(TestBucket), name, options)
        {
            TestBucketOutput = args.StringInput;
        }
    }

    public class TestMethodArgs
    {
        [Input("stringInput", required: true)]
        public Input<string> StringInput { get; set; } = default!;
    }

    public class ResourceProviderServiceTestProvider : Experimental.Provider.Provider
    {
        public static readonly CheckFailure CheckFailure = new CheckFailure("missing", "for testing");

        public const string DependentFieldName = "dependent";

        public static readonly ImmutableHashSet<Pulumi.Experimental.Provider.Urn> DependentUrns = ImmutableHashSet.Create(
            new Pulumi.Experimental.Provider.Urn("urn::some::resource"),
            new Pulumi.Experimental.Provider.Urn("urn::another::resource"));

        public const string BucketType = "bucket";
        public const string VirtualMachineType = "vm";
        public const string Tok = "tok";
        public const string Args = "args";

        public override Task<ConstructResponse> Construct(ConstructRequest request, CancellationToken ct)
        {
            return request.Type switch
            {
                BucketType => Construct<TestBucketArgs, TestBucket>(request, (name, args, options) => AsTask(new TestBucket(name, args, options))),
                _ => throw new NotImplementedException()
            };
        }

        public override Task<CallResponse> Call(Experimental.Provider.CallRequest request, CancellationToken ct)
        {
            IDictionary<string, PropertyValue>? response = new Dictionary<string, PropertyValue>()
            {
                {
                    Tok, new PropertyValue(request.Tok)
                },
                {
                    Args, new PropertyValue(request.Args)
                }
            };

            return AsTask(new CallResponse(response, new List<CheckFailure>() { CheckFailure },
                new Dictionary<string, ISet<Pulumi.Experimental.Provider.Urn>>() { { DependentFieldName, DependentUrns } }));
        }

        private async Task<T> AsTask<T>(T value)
        {
            await Task.Delay(100).ConfigureAwait(false);
            return value;
        }

        private async Task<ConstructResponse> Construct<TArgs, TResource>(
            ConstructRequest request,
            Func<string, TArgs, ComponentResourceOptions, Task<TResource>> factory
        )
            where TResource : ComponentResource
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var serializer = new PropertyValueSerializer();
#pragma warning restore CS0618 // Type or member is obsolete
            var args = await serializer.Deserialize<TArgs>(new PropertyValue(request.Inputs));
            var resource = await factory(request.Name, args, request.Options);

            var urn = await OutputUtilities.GetValueAsync(resource.Urn);
            if (string.IsNullOrEmpty(urn))
            {
                throw new InvalidOperationException($"URN of resource {request.Name} is not known.");
            }

            var stateValue = await serializer.StateFromComponentResource(resource);

            return new ConstructResponse(new Experimental.Provider.Urn(urn), stateValue, ImmutableDictionary<string, ISet<Experimental.Provider.Urn>>.Empty);
        }
    }
}
