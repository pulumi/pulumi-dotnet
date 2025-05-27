using Pulumi;

class EchoArgs : ResourceArgs
{
    [Input("value")]
    public Input<string> Value { get; set; } = null!;
}

class Echo : CustomResource
{
    public Echo(string name, EchoArgs args, CustomResourceOptions? opts = null) :
        base("testprovider:index:Echo", name, args, opts)
    {
    }
}

class ComponentArgs : ResourceArgs
{
    [Input("inputResource")]
    public Input<Echo> InputResource { get; set; } = null!;
}

class Component : ComponentResource
{
    [Output("outputResource")]
    public Output<Echo> OutputResource { get; protected set; } = null!;

    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts, remote: true)
    {
    }
}
