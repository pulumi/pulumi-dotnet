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
        public async void SupportsStackReferenceRequiredOutputs()
        {
            var mocks = new FakeStackOutputMocks("bucket", "my-bucket");
            var options = new TestOptions();

            var (resources, outputs) = await Deployment.TestAsync(mocks, options, () =>
            {
                var stackReference = new StackReference("my-stack");
                var output = stackReference.RequireOutput("bucket");

                return new Dictionary<string, object?>()
                {
                    ["bucket"] = output,
                };
            });

            var bucketOutput = outputs["bucket"] as Output<object>;
            Assert.NotNull(bucketOutput);
            var bucket = await bucketOutput!.DataTask;
            Assert.Equal("my-bucket", bucket.Value);
            Assert.False(bucket.IsSecret);
        }

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

            var bucketOutput = outputs["bucket"] as Task<StackReferenceOutputDetails>;
            Assert.NotNull(bucketOutput);
            var bucket = await bucketOutput!;
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

            var secretOutput = outputs["secret"] as Task<StackReferenceOutputDetails>;
            Assert.NotNull(secretOutput);
            var secret = await secretOutput!;
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

            var unknownOutput = outputs["unknown"] as Task<StackReferenceOutputDetails>;
            Assert.NotNull(unknownOutput);
            var unknown = await unknownOutput!;
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

        public Task<(string? id, object state)> NewResourceAsync(
            MockResourceArgs args)
        {
            if (args.Type == "pulumi:pulumi:StackReference")
            {
                var outputs = new Dictionary<string, object>();
                outputs[this.key] = this.val;

                var props = new Dictionary<string, object>();
                props["name"] = args.Inputs["name"];
                props["outputs"] = outputs;
                return Task.FromResult<(string? id, object state)>((args.Name + "-id", props));
            }
            else
            {
                throw new Exception($"Unknown resource {args.Type}");
            }
        }
    }
}
