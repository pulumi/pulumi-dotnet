// Copyright 2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi.Testing;
using Pulumi.Tests.Mocks;
using Xunit;

namespace Pulumi.Tests
{
    public class DeploymentSerializationErrorTests
    {
        [Fact]
        public async Task ResourceSerializationErrorIncludesPropertyName()
        {
            // `new object()` is not a supported argument type, so serializing the resource's inputs throws.
            var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await Deployment.TestAsync(new MyMocks(), new TestOptions(), () =>
                {
                    _ = new BadResource("res", new BadResourceArgs { Value = new object() });
                });
            });

            Assert.Contains("error serializing property \"value\"", exception.ToString());
        }

        [Fact]
        public async Task InvokeArgumentSerializationFailureIsLoggedAndRejects()
        {
            var engine = new MockEngine();
            Output<FunctionResult>? result = null;

            await Deployment.CreateRunnerAndRunAsync(
                () => new Deployment(engine, new MockMonitor(new MyMocks()), null),
                runner => runner.RunAsync(() =>
                {
                    result = Deployment.Instance.Invoke<FunctionResult>(
                        "mypkg::func", new BadInvokeArgs { Value = new object() });
                    return Task.FromResult((IDictionary<string, object?>)new Dictionary<string, object?>());
                }, null));

            Assert.NotNull(result);
            // Awaiting the output here also guarantees the serialization (and its error log) has completed.
            var exception = await Assert.ThrowsAnyAsync<Exception>(
                async () => await result!.GetValueAsync(new FunctionResult("")));
            Assert.Contains("error serializing property \"value\"", exception.Message);

            Assert.Contains(engine.Errors, e =>
                e.Contains("Error serializing arguments for invoke mypkg::func") &&
                e.Contains("error serializing property \"value\""));
        }

        [Fact]
        public async Task CallArgumentSerializationFailureIsLoggedAndRejects()
        {
            var engine = new MockEngine();
            Output<FunctionResult>? result = null;

            await Deployment.CreateRunnerAndRunAsync(
                () => new Deployment(engine, new MockMonitor(new MyMocks()), null),
                runner => runner.RunAsync(() =>
                {
                    result = Deployment.Instance.Call<FunctionResult>(
                        "mypkg::func", new BadCallArgs { Value = new object() });
                    return Task.FromResult((IDictionary<string, object?>)new Dictionary<string, object?>());
                }, null));

            Assert.NotNull(result);
            // Awaiting the output here also guarantees the serialization (and its error log) has completed.
            var exception = await Assert.ThrowsAnyAsync<Exception>(
                async () => await result!.GetValueAsync(new FunctionResult("")));
            Assert.Contains("error serializing property \"value\"", exception.Message);

            Assert.Contains(engine.Errors, e =>
                e.Contains("Error serializing arguments for call mypkg::func") &&
                e.Contains("error serializing property \"value\""));
        }

        public sealed class BadResourceArgs : ResourceArgs
        {
            [Input("value")]
            public object? Value { get; set; }
        }

        [ResourceType("test:index:BadResource", null)]
        private sealed class BadResource : CustomResource
        {
            public BadResource(string name, BadResourceArgs args, CustomResourceOptions? options = null)
                : base("test:index:BadResource", name, args, options)
            {
            }
        }

        public sealed class BadInvokeArgs : InvokeArgs
        {
            [Input("value")]
            public object? Value { get; set; }
        }

        public sealed class BadCallArgs : CallArgs
        {
            [Input("value")]
            public object? Value { get; set; }
        }

        [OutputType]
        public sealed class FunctionResult
        {
            [Output("testValue")]
            public string TestValue { get; set; }

            [OutputConstructor]
            public FunctionResult(string testValue)
            {
                TestValue = testValue;
            }
        }
    }
}
