using Pulumi;

namespace TestProvider;

class ComponentArgs : ResourceArgs
{
    [Input("testInput")]
    public Input<string> TestInput { get; set; } = null!;

    [Input("testSecretInput")]
    public Input<string> TestSecretInput { get; set; } = null!;

}

class Component : ComponentResource
{
    [Output("testOutput")]
    public Output<string> TestOutput { get; private set; }

    [Input("testSecretOutput")]
    public Input<string> TestSecretOutput { get; set; }

    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
        // we are accessing input.Length to make sure this fails if input is null
        TestOutput = args.TestInput.Apply(input => $"{input}-{input.Length}");
        TestSecretOutput = args.TestSecretInput.Apply(input => $"{input}-{input.Length}");
    }
}
