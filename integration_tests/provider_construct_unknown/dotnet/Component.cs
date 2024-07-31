using Pulumi;

class ComponentArgs : ResourceArgs
{
    [Input("testInput")]
    public Input<string> TestInput { get; set; } = null!;

    [Input("testSecretInput")]
    public Input<string> TestSecretInput { get; set; } = null!;
}

class Component : Pulumi.ComponentResource
{
    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts, remote: true)
    {
    }

    [Output("testOutput")]
    public Output<string> TestOutput { get; private set; } = default!;

    [Input("testSecretOutput")]
    public Input<string> TestSecretOutput { get; set; } = null!;
}
