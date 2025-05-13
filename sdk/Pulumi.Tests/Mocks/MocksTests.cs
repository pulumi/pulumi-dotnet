// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Pulumi.Testing;
using Xunit;

namespace Pulumi.Tests.Mocks
{
    class MyMocks : IMocks
    {
        public Task<object> CallAsync(MockCallArgs args)
        {
            return Task.FromResult<object>(args);
        }

        public static string InstancePublicIpAddress => "203.0.113.12";

        public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args) =>
            args.Type switch
            {
                "aws:ec2/instance:Instance" => Task.FromResult<(string?, object)>(("i-1234567890abcdef0", new Dictionary<string, object> { { "publicIp", InstancePublicIpAddress }, })),
                "pkg:index:MyCustom" => Task.FromResult<(string?, object)>((args.Name + "_id", args.Inputs)),
                _ => throw new Exception($"Unknown resource {args.Type}")
            };
    }

    class Issue8163Mocks : IMocks
    {
        public Task<object> CallAsync(MockCallArgs args)
        {
            throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unknown, "error code 404"));
        }

        public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args) => throw new Exception("Not used");
    }

    class MyInvalidMocks : IMocks
    {
        public Task<object> CallAsync(MockCallArgs args)
        {
            return Task.FromResult<object>(args);
        }

        public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args) =>
            args.Type switch
            {
                "aws:ec2/instance:Instance" => Task.FromResult<(string?, object)>(("i-1234567890abcdef0", new Dictionary<string, object> { { "publicIp", unchecked((int)0xcb00710c) }, })),
                "pkg:index:MyCustom" => Task.FromResult<(string?, object)>((args.Name + "_id", args.Inputs)),
                _ => throw new Exception($"Unknown resource {args.Type}")
            };
    }

    public class MocksTests
    {
        [Fact]
        public async Task TestCustom()
        {
            var resources = await Testing.RunAsync<MyStack>();

            var instance = resources.OfType<Instance>().FirstOrDefault();
            Assert.NotNull(instance);

            var ip = await instance!.PublicIp.GetValueAsync(whenUnknown: default!);
            Assert.Equal("203.0.113.12", ip);
        }

        [Fact]
        public async Task TestTwoOutputStack()
        {
            var resources = await Testing.RunAsync<TwoOutputStack>();

            var stack = resources.OfType<TwoOutputStack>().FirstOrDefault();
            Assert.NotNull(stack);

            var output1 = stack.Output1;
            Assert.NotNull(output1);
            Assert.Equal("output1", await output1.GetValueAsync(whenUnknown: default!));

            var output2 = stack.Output2;
            Assert.NotNull(output2);
            Assert.Equal("output2", await output2.GetValueAsync(whenUnknown: default!));
        }

        [Fact]
        public async Task TestCustomWithResourceReference()
        {
            var resources = await Testing.RunAsync<MyStack>();

            var myCustom = resources.OfType<MyCustom>().FirstOrDefault();
            Assert.NotNull(myCustom);

            var instance = await myCustom!.Instance.GetValueAsync(whenUnknown: default!);
            Assert.IsType<Instance>(instance);

            var ip = await instance.PublicIp.GetValueAsync(whenUnknown: default!);
            Assert.Equal("203.0.113.12", ip);
        }

        [Fact]
        public async Task TestStack()
        {
            var resources = await Testing.RunAsync<MyStack>();

            var stack = resources.OfType<MyStack>().FirstOrDefault();
            Assert.NotNull(stack);

            var ip = await stack!.PublicIp.GetValueAsync(whenUnknown: default!);
            Assert.Equal("203.0.113.12", ip);
        }

        /// Test for https://github.com/pulumi/pulumi/issues/8163
        [Fact]
        public async Task TestInvokeThrowing()
        {
            var (resources, exception) = await Testing.RunAsync(new Issue8163Mocks(), async () =>
            {

                var role = await GetRole.InvokeAsync(new GetRoleArgs()
                {
                    Name = "doesNotExistTypoEcsTaskExecutionRole"
                });

                var myInstance = new Instance("instance", new InstanceArgs());

                return new Dictionary<string, object?>()
                {
                    { "result", "x"},
                    { "instance", myInstance.PublicIp }
                };
            });

            var stack = resources.OfType<Stack>().FirstOrDefault();
            Assert.NotNull(stack);

            var instance = resources.OfType<Instance>().FirstOrDefault();
            Assert.Null(instance);

            Assert.NotNull(exception);
            Assert.StartsWith("Running program '", exception!.Message);
            Assert.Contains("' failed with an unhandled exception:", exception!.Message);
            Assert.Contains("Grpc.Core.RpcException: Status(StatusCode=\"Unknown\", Detail=\"error code 404\")", exception!.Message);
        }

        [Fact]
        public async Task TestInvokeToleratesUnknownsInPreview()
        {
            var resources = await Deployment.TestAsync<Issue8322.ReproStack>(
                new Issue8322.ReproMocks(),
                new TestOptions() { IsPreview = true }
            );
            var stack = resources.OfType<Issue8322.ReproStack>().Single();
            var result = await stack.Result.GetValueAsync(whenUnknown: "unknown!");
            Assert.Equal("unknown!", result);
        }

        [Fact]
        public async Task TestStackWithInvalidSchema()
        {
            var resources = await Deployment.TestAsync<MyStack>(new MyInvalidMocks(), new TestOptions { IsPreview = false });

            var stack = resources.OfType<MyStack>().FirstOrDefault();
            Assert.NotNull(stack);

            var ip = await stack!.PublicIp.GetValueAsync(whenUnknown: default!);
            Assert.Null(ip);

            // TODO: It would be good to assert that a warning was logged to the engine but getting hold of warnings requires re-plumbing what TestAsync returns.
        }

        private class NullOutputStack : Stack
        {
            [Output("foo")]
            public Output<string>? Foo { get; } = null;
        }

        [Fact]
        public async Task StackWithNullOutputsThrows()
        {
            try
            {
                await Testing.RunAsync<NullOutputStack>();
            }
            catch (Exception ex)
            {
                Assert.Contains(
                    "Pulumi.RunException: Output(s) 'foo' have no value assigned." +
                    " [Output] attributed properties must be assigned inside Stack constructor.",
                    ex.ToString());
                return;
            }

            throw new Exception("Expected to fail");
        }

        [Fact]
        public async Task TestUrnOutputPropertyIsNeverNull()
        {
            await Deployment.TestAsync<Issue7422.Issue7422Stack>(
                new Issue7422.Issue7422Mocks());
        }

        [Fact]
        public async Task TestAliases()
        {
            var mocks = new Aliases.AliasesMocks();
            var options = new TestOptions();
            var (resources, outputs) = await Deployment.TestAsync(mocks, options, () =>
            {
                var parent1 = new Pulumi.CustomResource("test:resource:type", "myres1", null, new CustomResourceOptions { });

                var child1Options = new CustomResourceOptions
                {
                    Parent = parent1,
                };

                var child1 = new Pulumi.CustomResource("test:resource:child", "myres1-child", null, child1Options);

                var parent2 = new Pulumi.CustomResource("test:resource:type", "myres2", null, new CustomResourceOptions { });

                var child2Options = new CustomResourceOptions
                {
                    Parent = parent2,
                    Aliases = { new Alias { Type = "test:resource:child2" } }
                };

                var child2 = new Pulumi.CustomResource("test:resource:child", "myres2-child", null, child2Options);

                var parent3 = new Pulumi.CustomResource("test:resource:type", "myres3", null, new CustomResourceOptions { });

                var child3Options = new CustomResourceOptions
                {
                    Parent = parent3,
                    Aliases = { new Alias { Name = "child2" } }
                };

                var child3 = new Pulumi.CustomResource("test:resource:child", "myres3-child", null, child3Options);

                var parent4 = new Pulumi.CustomResource("test:resource:type", "myres4", null, new CustomResourceOptions
                {
                    Aliases = { new Alias { Type = "test:resource:type3" } }
                });

                var child4Options = new CustomResourceOptions
                {
                    Parent = parent4,
                    Aliases = { new Alias { Name = "myres4-child2" } }
                };

                var child4 = new Pulumi.CustomResource("test:resource:child", "myres4-child", null, child4Options);

                var parent5 = new Pulumi.CustomResource("test:resource:type", "myres5", null, new CustomResourceOptions
                {
                    Aliases = { new Alias { Name = "myres52" } }
                });

                var child5Options = new CustomResourceOptions
                {
                    Parent = parent5,
                    Aliases = { new Alias { Name = "myres5-child2" } }
                };
                var child5 = new Pulumi.CustomResource("test:resource:child", "myres5-child", null, child5Options);

                var parent6 = new Pulumi.CustomResource("test:resource:type", "myres6", null, new CustomResourceOptions
                {
                    Aliases =
                    {
                        new Alias { Name = "myres62" },
                        new Alias { Type = "test:resource:type3" },
                        new Alias { Name = "myres63" },
                    }
                });

                var child6Options = new CustomResourceOptions
                {
                    Parent = parent6,
                    Aliases =
                    {
                        new Alias { Name = "myres6-child2" },
                        new Alias { Type = "test:resource:child2" }
                    }
                };

                var child6 = new Pulumi.CustomResource("test:resource:child", "myres6-child", null, child6Options);

                return new Dictionary<string, object?>
                {
                    [child1.GetResourceName()] = Deployment.AllAliases(
                        child1Options.Aliases,
                        child1.GetResourceName(),
                        child1.GetResourceType(),
                        child1Options.Parent),

                    [child2.GetResourceName()] = Deployment.AllAliases(
                        child2Options.Aliases,
                        child2.GetResourceName(),
                        child2.GetResourceType(),
                        child2Options.Parent),

                    [child3.GetResourceName()] = Deployment.AllAliases(
                        child3Options.Aliases,
                        child3.GetResourceName(),
                        child3.GetResourceType(),
                        child3Options.Parent),

                    [child4.GetResourceName()] = Deployment.AllAliases(
                        child4Options.Aliases,
                        child4.GetResourceName(),
                        child4.GetResourceType(),
                        child4Options.Parent),

                    [child5.GetResourceName()] = Deployment.AllAliases(
                        child5Options.Aliases,
                        child5.GetResourceName(),
                        child5.GetResourceType(),
                        child5Options.Parent),

                    [child6.GetResourceName()] = Deployment.AllAliases(
                        child6Options.Aliases,
                        child6.GetResourceName(),
                        child6.GetResourceType(),
                        child6Options.Parent),
                };
            });

            // TODO[pulumi/pulumi#8637]
            //
            // var parent1Urn = await resources[1].Urn.GetValueAsync("");
            // Assert.Equal("urn:pulumi:stack::project::test:resource:type::myres1", parent1Urn);

            // TODO[pulumi/pulumi#8637]: A subset of the "expected" below include the implicit root stack type
            // `pulumi:pulumi:Stack` as an explicit parent type in the URN. This should not happen, and indicates
            // a bug in the the Pulumi .NET SDK unrelated to Aliases.  It appears this only happens when using the
            // .NET mock testing framework, not when running normal programs.
            var expected = new Dictionary<string, List<string>>{
                { "myres1-child", new List<string>{}},
                { "myres2-child", new List<string>{
                    "urn:pulumi:stack::project::pulumi:pulumi:Stack$test:resource:type$test:resource:child2::myres2-child"
                }},
                { "myres3-child", new List<string>{
                    "urn:pulumi:stack::project::pulumi:pulumi:Stack$test:resource:type$test:resource:child::child2"
                }},
                { "myres4-child", new List<string>{
                    "urn:pulumi:stack::project::pulumi:pulumi:Stack$test:resource:type$test:resource:child::myres4-child2",
                    "urn:pulumi:stack::project::test:resource:type3$test:resource:child::myres4-child",
                    "urn:pulumi:stack::project::test:resource:type3$test:resource:child::myres4-child2",
                }},
                { "myres5-child", new List<string>{
                    "urn:pulumi:stack::project::pulumi:pulumi:Stack$test:resource:type$test:resource:child::myres5-child2",
                    "urn:pulumi:stack::project::test:resource:type$test:resource:child::myres52-child",
                    "urn:pulumi:stack::project::test:resource:type$test:resource:child::myres52-child2",
                }},
                { "myres6-child", new List<string>{
                    "urn:pulumi:stack::project::pulumi:pulumi:Stack$test:resource:type$test:resource:child::myres6-child2",
                    "urn:pulumi:stack::project::pulumi:pulumi:Stack$test:resource:type$test:resource:child2::myres6-child",
                    "urn:pulumi:stack::project::test:resource:type$test:resource:child::myres62-child",
                    "urn:pulumi:stack::project::test:resource:type$test:resource:child::myres62-child2",
                    "urn:pulumi:stack::project::test:resource:type$test:resource:child2::myres62-child",
                    "urn:pulumi:stack::project::test:resource:type3$test:resource:child::myres6-child",
                    "urn:pulumi:stack::project::test:resource:type3$test:resource:child::myres6-child2",
                    "urn:pulumi:stack::project::test:resource:type3$test:resource:child2::myres6-child",
                    "urn:pulumi:stack::project::test:resource:type$test:resource:child::myres63-child",
                    "urn:pulumi:stack::project::test:resource:type$test:resource:child::myres63-child2",
                    "urn:pulumi:stack::project::test:resource:type$test:resource:child2::myres63-child",
                }},
            };

            foreach (var resource in resources)
            {
                if (resource.GetResourceType() == "test:resource:child")
                {
                    var resourceName = resource.GetResourceName();
                    Assert.True(outputs.ContainsKey(resourceName), $"outputs contains aliases for resource {resourceName}");
                    if (outputs[resourceName] is ImmutableArray<Input<string>> computedAliases)
                    {
                        var actual = await Output.All(computedAliases).GetValueAsync(new ImmutableArray<string>());
                        var expectedAliases = expected[resourceName];
                        Assert.Equal(expectedAliases, actual);
                    }
                }
            }
        }
    }

    public static class Testing
    {
        public static Task<ImmutableArray<Resource>> RunAsync<T>() where T : Stack, new()
        {
            return Deployment.TestAsync<T>(new MyMocks(), new TestOptions { IsPreview = false });
        }

        public static Task<(ImmutableArray<Resource> Resources, Exception? Exception)> RunAsync(IMocks mocks, Func<Task<IDictionary<string, object?>>> func)
        {
            return Deployment.TryTestAsync(mocks, runner => runner.RunAsync(func, null), new TestOptions { IsPreview = false });
        }
    }
}
