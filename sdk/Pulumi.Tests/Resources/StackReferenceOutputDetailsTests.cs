// Copyright 2016-2023, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi.Testing;
using Xunit;

namespace Pulumi.Tests.Resources
{
    public class StackReferenceOutputDetailsTests
    {
        [Fact]
        public async void SupportsPlainText()
        {
            var mocks = new FakeStackOutputMocks("bucket", "my-bucket");
            var options = new TestOptions();

            var (resources, outputs) = await Deployment.TestAsync(mocks, options, () =>
            {
                var stackReference = new StackReference("my-stack");
                return new Dictionary<string, object?>()
                {
                    ["bucket"] = stackReference.GetOutputDetailsAsync("bucket"),
                };
            });

            var bucket = await (outputs["bucket"] as Task<StackReferenceOutputDetails>);
            Assert.Equal("my-bucket", bucket.Value);
            Assert.Null(bucket.SecretValue);
        }

        [Fact]
        public async void SupportsSecrets()
        {
            var mocks = new FakeStackOutputMocks("secret", Output.CreateSecret("my-bucket"));
            var options = new TestOptions();

            var (resouces, outputs) = await Deployment.TestAsync(mocks, options, () =>
            {
                var stackReference = new StackReference("my-stack");
                return new Dictionary<string, object?>()
                {
                    ["secret"] = stackReference.GetOutputDetailsAsync("secret"),
                };
            });

            var secret = await (outputs["secret"] as Task<StackReferenceOutputDetails>);
            Assert.Null(secret.Value);
            Assert.Equal("my-bucket", secret.SecretValue);
        }

        [Fact]
        public async void Unknowns()
        {
            var mocks = new FakeStackOutputMocks("something", "foo");
            var options = new TestOptions();

            var (resources, outputs) = await Deployment.TestAsync(mocks, options, () =>
            {
                var stackReference = new StackReference("my-stack");
                return new Dictionary<string, object?>()
                {
                    ["unknown"] = stackReference.GetOutputDetailsAsync("unknown"),
                };
            });

            var unknown = await (outputs["unknown"] as Task<StackReferenceOutputDetails>);
            Assert.Null(unknown.Value);
            Assert.Null(unknown.SecretValue);
        }
    }

    class FakeStackOutputMocks : IMocks
    {
        private string key;
        private object val;
        public FakeStackOutputMocks(string key, object val)
        {
            this.key = key;
            this.val = val;
        }

        public Task<object> CallAsync(MockCallArgs args)
        {
            return Task.FromResult<object>(args);
        }

        public async Task<(string? id, object state)> NewResourceAsync(
            MockResourceArgs args)
        {
            if (args.Type == "pulumi:pulumi:StackReference")
            {
                var outputs = new Dictionary<string, object>();
                outputs[this.key] = this.val;

                var props = new Dictionary<string, object>();
                props["outputs"] = outputs;
                return (args.Name + "-id", props);
            }
            else
            {
                throw new Exception($"Unknown resource {args.Type}");
            }
        }
    }
}
