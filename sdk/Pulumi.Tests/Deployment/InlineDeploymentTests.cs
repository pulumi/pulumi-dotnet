using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi.Testing;
using Xunit;

namespace Pulumi.Tests;

public class InlineDeploymentTests
{
    [Fact]
    public async Task InlineDeploymentAwaitsTasks()
    {
        var mocks = new AwaitingMocks();
        var monitor = new MockMonitor(mocks);
        var res = TryInline<Component>(monitor, async () =>
        {
            var stack = new Stack();
            await Task.Delay(1);
            return new Component("test", null, new ComponentResourceOptions
            {
                Parent = stack
            });
        });

        await Task.WhenAny(res, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.False(res.IsCompleted);
        mocks.Complete();
        await res;
        Assert.Equal(4, monitor.Resources.Count);
    }

    internal static async Task<T> TryInline<T>(IMonitor monitor, Func<Task<T>> runAsync)
    {
        var engine = new MockEngine();

        var deploymentBuilder = new MockDeploymentBuilder(engine, monitor);

        var inlineDeploymentSettings = new InlineDeploymentSettings(null, "1", "1", new Dictionary<string, string>(),
            new List<string>(), "organization", "project", "stack", 0, false);

        return await Deployment.RunInlineAsyncWithResult(deploymentBuilder, inlineDeploymentSettings, runAsync)
            .ConfigureAwait(false);
    }

    internal class Component : ComponentResource
    {
        public Component(string name, ResourceArgs? args, ComponentResourceOptions? options = null, bool remote = false)
            : base("test:res:a", name, args, options, remote)
        {
            new ComponentResource("test:res:b", "testB", new ComponentResourceOptions
            {
                Parent = this
            });
            new ComponentResource("test:res:b", "testC", new ComponentResourceOptions
            {
                Parent = this
            });
        }
    }

    internal class AwaitingMocks : IMocks
    {
        private readonly TaskCompletionSource _taskCompletionSource = new();

        private Task Task => _taskCompletionSource.Task;

        public void Complete()
        {
            _taskCompletionSource.TrySetResult();
        }

        public async Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
        {
            if (args.Type?.Equals("test:res:b") ?? false)
            {
                await Task;
            }

            string? id = args.Name;
            return (id, ImmutableDictionary.Create<string, string>());
        }

        public Task<object> CallAsync(MockCallArgs args)
        {
            return Task.FromResult(new object());
        }
    }
}
