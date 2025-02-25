using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Pulumi.Experimental.Provider;

namespace Pulumi.Tests.Provider
{
    public class ComponentProviderTests
    {
        private ComponentProvider _provider;

        public ComponentProviderTests()
        {
            var assembly = typeof(TestComponent).Assembly;
            _provider = new ComponentProvider(assembly, "test-package", new[] { typeof(TestComponent) });
        }

        [Fact]
        public async Task GetSchema_ShouldReturnValidSchema()
        {
            var request = new GetSchemaRequest(1, null, null);
            var response = await _provider.GetSchema(request, CancellationToken.None);

            Assert.NotNull(response);
            Assert.NotNull(response.Schema);
            Assert.Contains("TestComponent", response.Schema);
            Assert.Contains("testProperty", response.Schema);
        }

        [Fact]
        public async Task Construct_ValidComponent_ShouldThrowExpectedDeploymentException()
        {
            var name = "test-component";
            var inputs = new Dictionary<string, PropertyValue>
            {
                ["testProperty"] = new PropertyValue("test-value")
            }.ToImmutableDictionary();
            var options = new ComponentResourceOptions();
            var request = new ConstructRequest(
                "test-package:index:TestComponent",
                name,
                inputs,
                options
            );

            // We haven't initiated the deployment, so component construction will fail. Expect that specific exception,
            // since it will indicate that the rest of the provider is working. The checks are somewhat brittle and may fail
            // if we change the exception message or stack, but hopefully that will not happen too often.
            var exception = await Assert.ThrowsAsync<TargetInvocationException>(
                async () => await _provider.Construct(request, CancellationToken.None)
            );

            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("Deployment", exception.InnerException!.Message);
        }

        [Fact]
        public async Task Construct_InvalidPackageName_ShouldThrowException()
        {
            var request = new ConstructRequest(
                "wrong:index:TestComponent",
                "test",
                ImmutableDictionary<string, PropertyValue>.Empty,
                new ComponentResourceOptions()
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _provider.Construct(request, CancellationToken.None)
            );

            Assert.Contains("Invalid resource type", exception.Message);
        }

        [Fact]
        public async Task Construct_NonExistentComponent_ShouldThrowException()
        {
            var request = new ConstructRequest(
                "test-package:index:NonExistentComponent",
                "test",
                ImmutableDictionary<string, PropertyValue>.Empty,
                new ComponentResourceOptions()
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _provider.Construct(request, CancellationToken.None)
            );

            Assert.Contains("Component type not found", exception.Message);
        }
    }

    class TestComponentArgs : ResourceArgs
    {
        [Input("testProperty", required: true)]
        public Input<string> TestProperty { get; set; } = null!;
    }

    class TestComponent : ComponentResource
    {
        public TestComponent(string name, TestComponentArgs args, ComponentResourceOptions? options = null)
            : base("test-package:index:TestComponent", name, args, options)
        {
        }
    }
}
