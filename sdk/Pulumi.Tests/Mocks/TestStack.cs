// Copyright 2016-2020, Pulumi Corporation

using System;

namespace Pulumi.Tests.Mocks
{
    [ResourceType("aws:ec2/instance:Instance", null)]
    public class Instance : CustomResource
    {
        [Output("publicIp")]
        public Output<string> PublicIp { get; private set; } = null!;

        public Instance(string name, InstanceArgs args, CustomResourceOptions? options = null)
            : base("aws:ec2/instance:Instance", name, args, options)
        {
        }
    }

    public sealed class InstanceArgs : ResourceArgs
    {
    }

    public class MyCustom : CustomResource
    {
        [Output("instance")]
        public Output<Instance> Instance { get; private set; } = null!;

        public MyCustom(string name, MyCustomArgs args, CustomResourceOptions? options = null)
            : base("pkg:index:MyCustom", name, args, options)
        {
        }
    }

    public sealed class MyCustomArgs : ResourceArgs
    {
        [Input("instance")]
        public Input<Instance>? Instance { get; set; }
    }

    public class MyStack : Stack
    {
        [Output("publicIp")]
        public Output<string> PublicIp { get; private set; }

        public MyStack()
        {
            var myInstance = new Instance("instance", new InstanceArgs());
            new MyCustom("mycustom", new MyCustomArgs { Instance = myInstance });
            this.PublicIp = myInstance.PublicIp;
        }
    }

    // Regression test data for https://github.com/pulumi/pulumi-dotnet/issues/594
    public class TwoOutputStack : Stack
    {
        [Output("output1")]
        public Output<string> Output1 { get; set; } = Output.Create<string>("output1");

        [Output("output2")]
        public Output<string> Output2 { get; set; }

        public TwoOutputStack()
        {
            Output2 = Output.Create<string>("output2");
        }
    }
}
