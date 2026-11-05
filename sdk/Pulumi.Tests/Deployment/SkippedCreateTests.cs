// Copyright 2026, Pulumi Corporation

using System.Collections.Generic;
using System.Threading.Tasks;

using Pulumirpc;
using Xunit;

using Pulumi.Testing;

namespace Pulumi.Tests
{
    public class SkippedCreateTests
    {
        [Fact]
        public async Task SkippedCreateResolvesOutputsAsUnknown()
        {
            // https://github.com/pulumi/pulumi-dotnet/issues/1057
            var applyRan = false;
            Output<int>? length = null;

            await Pulumi.Deployment.CreateRunnerAndRunAsync(
                () => new Pulumi.Deployment(new MockEngine(), new SkippedCreateMonitor(new SkippedCreateMocks()), new TestOptions { IsPreview = false }),
                runner => runner.RunAsync(() =>
                {
                    var resource = new SkippedCustomResource("res");
                    length = resource.Value.Apply(v =>
                    {
                        applyRan = true;
                        return v.Length;
                    });
                    return Task.FromResult((IDictionary<string, object?>)new Dictionary<string, object?>());
                }, null));

            Assert.NotNull(length);
            Assert.Equal(-1, await length!.GetValueAsync(whenUnknown: -1));
            Assert.False(applyRan);
        }

        private sealed class SkippedCreateMonitor : MockMonitor
        {
            public SkippedCreateMonitor(IMocks mocks)
                : base(mocks)
            {
            }

            public override async Task<RegisterResourceResponse> RegisterResourceAsync(Resource resource, RegisterResourceRequest request)
            {
                var response = await base.RegisterResourceAsync(resource, request);
                response.Unknown = true;
                return response;
            }
        }

        public sealed class SkippedArgs : ResourceArgs
        {
        }

        private class SkippedCustomResource : CustomResource
        {
            [Output("value")]
            public Output<string> Value { get; private set; } = null!;

            public SkippedCustomResource(string name)
                : base("test:SkippedCreateTests:resource", name, new SkippedArgs(), options: null)
            {
            }
        }

        class SkippedCreateMocks : IMocks
        {
            public Task<object> CallAsync(MockCallArgs args)
            {
                return Task.FromResult<object>(args);
            }

            public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
            {
                return Task.FromResult<(string?, object)>((null, new Dictionary<string, object>()));
            }
        }
    }
}
