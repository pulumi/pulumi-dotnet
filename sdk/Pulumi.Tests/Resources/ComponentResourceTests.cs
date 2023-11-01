// Copyright 2016-2023, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Pulumi.Testing;
using Xunit;

namespace Pulumi.Tests.Resources
{
    public class ComponentResourceTests
    {
        // A ComponentResource cannot use a Provider that was passed in.
        // However, it should still propagate the Provider to its children.
        [Fact]
        public async void PropagatesProviderOption()
        {
            var mocks = new MinimalMocks();
            var options = new TestOptions();

            await Deployment.TestAsync(mocks, options, () =>
            {
                var provider = new ProviderResource(
                    "test",
                    "prov",
                    ResourceArgs.Empty
                );

                // The Provider is passed in to the ComponentResource,
                // but it cannot use it.
                var component = new ComponentResource(
                    "custom:foo:component",
                    "comp",
                    new ComponentResourceOptions
                    {
                        Provider = provider,
                    }
                );

                // This CustomResource, however, should use the Provider.
                // It should receive it from the parent ComponentResource.
                var custom = new CustomResource(
                    "test:index:MyResource",
                    "custom",
                    ResourceArgs.Empty,
                    new CustomResourceOptions
                    {
                        Parent = component,
                    }
                );

                Assert.Equal(provider, custom._provider);
            });
        }

        // A ComponentResource should propagate the bag of Providers
        // it receives via the Provider option to its children.
        [Fact]
        public async void PropagatesProvidersOption()
        {
            var mocks = new MinimalMocks();
            var options = new TestOptions();

            await Deployment.TestAsync(mocks, options, () =>
            {
                var provider = new ProviderResource(
                    "test",
                    "prov",
                    ResourceArgs.Empty
                );

                // The Provider is passed in to the ComponentResource
                // via the Providers option, but it cannot use it.
                var component = new ComponentResource(
                    "custom:foo:component",
                    "comp",
                    new ComponentResourceOptions
                    {
                        Providers = new List<ProviderResource>
                        {
                            provider,
                        }
                    }
                );

                // This CustomResource should receive the Provider
                // from the parent ComponentResource.
                var custom = new CustomResource(
                    "test:index:MyResource",
                    "custom",
                    ResourceArgs.Empty,
                    new CustomResourceOptions
                    {
                        Parent = component,
                    }
                );

                Assert.Equal(provider, custom._provider);
            });
        }

        class BasicComponent : ComponentResource
        {
            public Output<string> First { get; set; }
            [Output]
            public Output<string> Second { get; set; }
            [Output("myThird")]
            public Output<string> Third { get; set; }
            public BasicComponent(string name) : base("token:token:token", name, ResourceArgs.Empty)
            {
                First = Output.Create("first");
                Second = Output.Create("second");
                Third = Output.Create("third");
                RegisterOutputs();
            }
        }

        [Fact]
        public async Task RegisterOutputsCorrectlyUsesReflectionToRegisterOutputProperties()
        {
            var mocks = new MinimalMocks();
            var options = new TestOptions();
            var resources = await Deployment.TestAsync(mocks, options, () =>
            {
                new BasicComponent("basic");
            });

            foreach (var resource in resources)
            {
                if (resource is BasicComponent basic)
                {
                    var urn = await basic.Urn.GetValueAsync("<unknown>");
                    var outputs = mocks.GetRegisteredOutputs(urn) as IDictionary<string, object>;
                    Assert.Contains("Second", outputs);
                    Assert.Contains("myThird", outputs);

                    if (outputs["Second"] is Output<string> second)
                    {
                        var value = await second.GetValueAsync("<unknwon>");
                        Assert.Equal("second", value);
                    }

                    if (outputs["myThird"] is Output<string> third)
                    {
                        var value = await third.GetValueAsync("<unknwon>");
                        Assert.Equal("third", value);
                    }
                }
            }
        }
    }

    class MinimalMocks : IMocks
    {
        private readonly Dictionary<string, ImmutableDictionary<string, object>> _registeredOutputsByUrn;

        public MinimalMocks()
        {
            _registeredOutputsByUrn = new Dictionary<string, ImmutableDictionary<string, object>>();
        }

        public Task<object> CallAsync(MockCallArgs args)
        {
            return Task.FromResult<object>(args);
        }

        public async Task<(string? id, object state)> NewResourceAsync(
            MockResourceArgs args)
        {
            return (args.Name + "-id", args.Inputs);
        }

        public Task RegisterResourceOutputs(MockRegisterResourceOutputsRequest request)
        {
            _registeredOutputsByUrn[request.Urn ?? ""] = request.Outputs;
            return Task.CompletedTask;
        }

        public ImmutableDictionary<string, object> GetRegisteredOutputs(string urn)
        {
            return _registeredOutputsByUrn[urn];
        }
    }
}
