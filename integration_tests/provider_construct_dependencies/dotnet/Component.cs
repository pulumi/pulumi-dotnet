using Pulumi;

class Component : Pulumi.ComponentResource
{
    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts, remote: true)
    {
    }

    [Output("testOutput")]
    public Output<string> TestOutput { get; private set; } = default!;
}

class ComponentArgs : ResourceArgs
{
    [Input("testInput")]
    public Input<string> TestInput { get; set; } = null!;
}
