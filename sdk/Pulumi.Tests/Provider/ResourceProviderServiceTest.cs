using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using FluentAssertions;
using Pulumi.Experimental.Provider;
using Xunit;
using Xunit.Abstractions;
using CallRequest = Pulumirpc.CallRequest;
using Constants = Pulumi.Serialization.Constants;

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

        var args = new TestBucketArgs()
        {
            StringInput = "Hello World"
        };

        var serializeArga = await Serialize(args);

        var stringInputName = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(nameof(args.StringInput));
        var urnDependentResource = "urn::dependent::resource";
        var constructRequest = new Pulumirpc.ConstructRequest()
        {
            Inputs = serializeArga.Value.StructValue,
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
        if (serializeArga.Dependencies != null)
        {
            constructRequest.Dependencies.AddRange(serializeArga.Dependencies.Select(urn => urn.ToString()));
        }

        var constructResponse = await provider.ConstructAsync(constructRequest);

        Assert.Equal(constructResponse.Urn, $"urn:pulumi:{constructRequest.Stack}::{constructRequest.Project}::{nameof(TestBucket)}::{constructRequest.Name}");
        constructResponse.State.Fields.Should().ContainSingle(nameof(TestBucket.TestBucketOutput), await args.StringInput.ToOutput().GetValueAsync(string.Empty));
        constructResponse.StateDependencies.Should().ContainSingle(stringInputName, new Pulumirpc.ConstructResponse.Types.PropertyDependencies { Urns = { urnDependentResource } });
    }

    private static async Task<PropertyValue.ValueWithDependencies> Serialize(object args)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var propertyValueSerializer = new PropertyValueSerializer();
#pragma warning restore CS0618 // Type or member is obsolete
        var marshalled = PropertyValue.Marshal(await propertyValueSerializer.Serialize(args));

        return marshalled;
    }


    [Theory]
    [InlineData(true, true, 1)]
    // [InlineData(false, false, 3)]
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
            Args = serializeArgs.Value.StructValue,
            ArgDependencies = { },
            AcceptsOutputValues = acceptsOutputValues
        };
        if (serializeArgs.Dependencies != null)
        {
            foreach (var arg in serializeArgs.Value.StructValue.Fields)
            {
                if (arg.Value.StructValue == null || !arg.Value.StructValue.Fields.TryGetValue(Constants.DependenciesName, out var dependencies))
                {
                    continue;
                }

                var argumentDependencies = new CallRequest.Types.ArgumentDependencies();
                argumentDependencies.Urns.AddRange(dependencies.ListValue.Values.Select(v => v.StringValue));
                callRequest.ArgDependencies.Add(arg.Key, argumentDependencies);
            }
        }

        var callResponse = await provider.CallAsync(callRequest);
        Assert.True(callResponse.Return.Fields.TryGetValue(ResourceProviderServiceTestProvider.Tok, out var tokResponse));
        Assert.Equal(tokResponse!.StringValue, callRequest.Tok);
        Assert.True(callResponse.Return.Fields.TryGetValue(ResourceProviderServiceTestProvider.Args, out var argsResponse));
        argsResponse.Should().BeEquivalentTo(serializeArgs.Value);
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

    public class ResourceProviderServiceTestProvider : ComponentResourceProviderBase
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
                BucketType => Construct<TestBucket, TestBucketArgs>(request, (name, args, options) => AsTask(new TestBucket(name, args, options))),
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

        private Task<ConstructResponse> Construct<TResource, TArgs>(
            ConstructRequest request,
            Func<string, TArgs, ComponentResourceOptions, TResource> factory
        )
            where TResource : ComponentResource
        {
            return Construct<TResource, TArgs>(
                request: request,
                factory: (name, args, options) => Task.FromResult(factory(name, args, options)));
        }
    }
}
