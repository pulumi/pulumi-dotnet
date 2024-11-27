// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Xunit;

using Pulumi.Testing;
using Pulumi.Tests.Mocks;

namespace Pulumi.Tests
{
    public class DeploymentInvokeDependsOnTests
    {
        [Fact]
        public async Task DeploymentInvokeDependsOn()
        {

            var mocks = new InvokeMocks();
            var testOptions = new TestOptions();
            var (resources, outputs) = await Deployment.TestAsync(mocks, testOptions, () =>
            {
                var resource = new MyCustomResource("some-resource", null, new CustomResourceOptions());
                var deps = new InputList<Resource>();
                deps.Add(resource);

                var resultOutput = TestFunction.Invoke(new FunctionArgs(), new InvokeOutputOptions { DependsOn = deps }).Apply(result =>
                {
                    Assert.Equal("my value", result.TestValue);
                    Assert.True(mocks.Resolved); // The resource should have been awaited
                    return result;
                });

                return new Dictionary<string, object?>
                {
                    ["functionResult"] = resultOutput,
                };
            });

            if (outputs["functionResult"] is Output<FunctionResult> functionResult)
            {

                // functionResult

                var value = await functionResult.GetValueAsync(new FunctionResult(""));
                Assert.Equal("my value", value.TestValue);
            }
            else
            {
                throw new Exception($"Expected result to be of type Output<FunctionResult>");
            }
        }

        public sealed class MyArgs : ResourceArgs { }

        [ResourceType("test:DeploymentInvokeDependsOnTests:resource", null)]
        private class MyCustomResource : CustomResource
        {
            public MyCustomResource(string name, MyArgs? args, CustomResourceOptions? options = null)
                : base("test:DeploymentInvokeDependsOnTests:resource", name, args ?? new MyArgs(), options)
            {
            }
        }

        public sealed class FunctionArgs : global::Pulumi.InvokeArgs { }

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

        public static class TestFunction
        {
            public static Output<FunctionResult> Invoke(FunctionArgs args, InvokeOptions? options = null)
                => global::Pulumi.Deployment.Instance.Invoke<FunctionResult>("mypkg::func", args ?? new FunctionArgs(), options);
        }

        class InvokeMocks : IMocks
        {
            public bool Resolved = false;


            public Task<object> CallAsync(MockCallArgs args)
            {
                return Task.FromResult<object>(new Dictionary<string, object>()
                {
                    ["testValue"] = "my value"
                });
            }

            public async Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
            {
                await Task.Delay(3000);
                Resolved = true;
                return ("id", new Dictionary<string, object>());
            }
        }
    }
}
