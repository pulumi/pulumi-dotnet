// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace Pulumi.Tests
{
    public class StackTests
    {
        private class ValidStack : Stack
        {
            [Output("foo")]
            public Output<string> ExplicitName { get; set; }

            [Output]
            public Output<string> ImplicitName { get; set; }

            public ValidStack()
            {
                this.ExplicitName = Output.Create("bar");
                this.ImplicitName = Output.Create("buzz");
            }
        }

        [Fact]
        public async Task ValidStackInstantiationSucceeds()
        {
            var (stack, outputs) = await Run<ValidStack>();
            Assert.Equal(2, outputs.Count);
            Assert.Same(stack.ExplicitName, outputs["foo"]);
            Assert.Same(stack.ImplicitName, outputs["ImplicitName"]);
        }

        private class InvalidOutputTypeStack : Stack
        {
            [Output("foo")]
            public string Foo { get; set; }

            public InvalidOutputTypeStack()
            {
                this.Foo = "bar";
            }
        }

        [Fact]
        public async Task StackWithInvalidOutputTypeThrows()
        {
            try
            {
                await Run<InvalidOutputTypeStack>();
            }
            catch (RunException ex)
            {
                Assert.Contains("Output(s) 'foo' have incorrect type", ex.ToString());
                return;
            }

            throw new XunitException("Should not come here");
        }

        private class ValidDerivedStack : ValidStack
        {
            [Output("child")]
            public Output<string> ChildProperty { get; set; }

            public ValidDerivedStack()
            {
                this.ChildProperty = Output.Create("hello from derived stack");
            }
        }

        [Fact]
        public async Task ValidDerivedStackInstantiationSucceeds()
        {
            var (stack, outputs) = await Run<ValidDerivedStack>();
            Assert.Equal(3, outputs.Count);
            Assert.Same(stack.ExplicitName, outputs["foo"]);
            Assert.Same(stack.ImplicitName, outputs["ImplicitName"]);
            Assert.Same(stack.ChildProperty, outputs["child"]);
        }

        private async Task<(T, IDictionary<string, object?>)> Run<T>() where T : Stack, new()
        {
            // Arrange
            Output<IDictionary<string, object?>>? outputs = null;

            var runner = new Mock<IRunner>(MockBehavior.Strict);
            runner.Setup(r => r.RegisterTask(It.IsAny<string>(), It.IsAny<Task>()));

            var mock = new Mock<IDeploymentInternal>(MockBehavior.Strict);
            mock.Setup(d => d.ProjectName).Returns("TestProject");
            mock.Setup(d => d.StackName).Returns("TestStack");
            mock.Setup(d => d.Runner).Returns(runner.Object);
            mock.SetupSet(content => content.Stack = It.IsAny<Stack>());
            mock.Setup(d => d.ReadOrRegisterResource(It.IsAny<Stack>(), It.IsAny<bool>(),
                It.IsAny<Func<string, Resource>>(), It.IsAny<ResourceArgs>(),
                It.IsAny<ResourceOptions>(),
                It.IsAny<RegisterPackageRequest?>()));
            mock.Setup(d => d.RegisterResourceOutputs(It.IsAny<Stack>(), It.IsAny<Output<IDictionary<string, object?>>>()))
                .Callback((Resource _, Output<IDictionary<string, object?>> o) => outputs = o);

            mock.Setup(d => d.Logger).Returns(new TestDeploymentLogger());

            Deployment.Instance = new DeploymentInstance(mock.Object);

            // Act
            var stack = CreateInstance<T>();
            stack.RegisterPropertyOutputs();

            // Assert
            Assert.NotNull(outputs);
            var values = await outputs!.DataTask;
            return (stack, values.Value);
        }

        private T CreateInstance<T>()
            => (T)(typeof(T).GetConstructor(Type.EmptyTypes)!.Invoke(
                       BindingFlags.DoNotWrapExceptions,
                       null, null, null)!);
    }
}
