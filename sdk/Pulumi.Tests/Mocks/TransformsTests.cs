// Copyright 2026, Pulumi Corporation

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Testing;
using Xunit;

namespace Pulumi.Tests.Mocks
{
    public class TransformsTests
    {
        private sealed class RecordingMocks : IMocks
        {
            public readonly List<ResourceTransform> StackTransforms = new List<ResourceTransform>();
            public readonly List<InvokeTransform> InvokeTransforms = new List<InvokeTransform>();
            public readonly List<ResourceTransform> ResourceTransforms = new List<ResourceTransform>();
            public ImmutableDictionary<string, object>? Inputs;

            public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
            {
                if (args.Name == "res")
                {
                    Inputs = args.Inputs;
                    ResourceTransforms.AddRange(args.Transforms);
                }
                return Task.FromResult<(string?, object)>(($"{args.Name}_id", args.Inputs));
            }

            public Task<object> CallAsync(MockCallArgs args)
                => Task.FromResult<object>(args.Args);

            public Task RegisterTransform(ResourceTransform transform)
            {
                StackTransforms.Add(transform);
                return Task.CompletedTask;
            }

            public Task RegisterInvokeTransform(InvokeTransform transform)
            {
                InvokeTransforms.Add(transform);
                return Task.CompletedTask;
            }
        }

        private sealed class TransformsTestResourceArgs : ResourceArgs
        {
            [Input("foo")]
            public Input<string>? Foo { get; set; }
        }

        private sealed class TransformsTestResource : CustomResource
        {
            public TransformsTestResource(string name, TransformsTestResourceArgs args, CustomResourceOptions? options = null)
                : base("test:index:TransformsTestResource", name, args, options)
            {
            }
        }

        private static Task<ResourceTransformResult?> StackTransform(ResourceTransformArgs args, CancellationToken cancellationToken)
        {
            var newArgs = args.Args.SetItem("foo", "stack");
            return Task.FromResult<ResourceTransformResult?>(new ResourceTransformResult(newArgs, args.Options));
        }

        private static Task<ResourceTransformResult?> OwnTransform(ResourceTransformArgs args, CancellationToken cancellationToken)
        {
            var newArgs = args.Args.SetItem("foo", "own");
            return Task.FromResult<ResourceTransformResult?>(new ResourceTransformResult(newArgs, args.Options));
        }

        private static Task<InvokeTransformResult?> AddExtraInvokeTransform(InvokeTransformArgs args, CancellationToken cancellationToken)
        {
            var newArgs = args.Args.SetItem("extra", "added");
            return Task.FromResult<InvokeTransformResult?>(new InvokeTransformResult(newArgs, args.Options));
        }

        private sealed class TransformsStack : Stack
        {
            public TransformsStack() : base(new StackOptions
            {
                ResourceTransforms = { StackTransform },
            })
            {
                _ = new TransformsTestResource("res", new TransformsTestResourceArgs { Foo = "orig" }, new CustomResourceOptions
                {
                    ResourceTransforms = { OwnTransform },
                });
            }
        }

        [Fact]
        public async Task TransformsAreDeliveredButNotRun()
        {
            var mocks = new RecordingMocks();
            await Deployment.TestAsync<TransformsStack>(mocks, new TestOptions { IsPreview = false });

            Assert.NotNull(mocks.Inputs);
            Assert.Equal("orig", mocks.Inputs!["foo"]);

            Assert.Single(mocks.StackTransforms);
            Assert.Single(mocks.ResourceTransforms);

            var args = new ResourceTransformArgs(
                "res",
                "test:index:TransformsTestResource",
                custom: true,
                ImmutableDictionary<string, object?>.Empty.Add("foo", "orig"),
                new CustomResourceOptions());

            var ownResult = await mocks.ResourceTransforms[0](args);
            Assert.NotNull(ownResult);
            Assert.Equal("own", ownResult!.Value.Args["foo"]);

            var stackResult = await mocks.StackTransforms[0](args);
            Assert.NotNull(stackResult);
            Assert.Equal("stack", stackResult!.Value.Args["foo"]);
        }

        [Fact]
        public async Task InvokeTransformsAreDelivered()
        {
            var mocks = new RecordingMocks();
            var (_, exception) = await Deployment.TryTestAsync(mocks, runner => runner.RunAsync(async () =>
            {
                Deployment.Instance.RegisterInvokeTransform(AddExtraInvokeTransform);
                await ((Deployment)Deployment.InternalInstance).AwaitPendingRegistrations().ConfigureAwait(false);
                return (IDictionary<string, object?>)new Dictionary<string, object?>();
            }, null), new TestOptions { IsPreview = false });

            Assert.Null(exception);
            Assert.Single(mocks.InvokeTransforms);

            var result = await mocks.InvokeTransforms[0](new InvokeTransformArgs(
                "test:index:MyFunction",
                ImmutableDictionary<string, object?>.Empty.Add("orig", "value"),
                new InvokeOptions()));
            Assert.NotNull(result);
            Assert.Equal("added", result!.Value.Args["extra"]);
            Assert.Equal("value", result.Value.Args["orig"]);
        }
    }
}
