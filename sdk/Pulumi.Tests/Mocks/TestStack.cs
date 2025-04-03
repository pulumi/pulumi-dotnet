// Copyright 2016-2020, Pulumi Corporation

using System.Collections.Immutable;

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

    [OutputType]
    public sealed class ObjectMeta
    {
        public readonly ImmutableDictionary<string, string> Labels;

        [OutputConstructor]
        private ObjectMeta(ImmutableDictionary<string, string> labels){
            Labels = labels;
        }
    }


    public class CustomMap : CustomResource
    {
        [Output("metadata")]
        public Output<ObjectMeta> Metadata { get; private set; } = null!;

        public CustomMap(string name, CustomResourceOptions? options = null)
            : base("pkg:index:CustomMap", name, null, options)
        {
        }
    }

}
