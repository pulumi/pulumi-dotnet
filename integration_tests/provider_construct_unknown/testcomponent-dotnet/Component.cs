using System.Threading.Tasks;
using Pulumi;
using Pulumi.Utilities;

namespace TestProvider;

class ComponentArgs : ResourceArgs
{
    [Input("testInput")]
    public Input<string> TestInput { get; set; } = null!;

}

[OutputType]
public sealed class ComplexTypeOutput
{
    [Output("name")]
    public string Name { get; set; }

    [OutputConstructor]
    public ComplexTypeOutput(string name)
    {
        Name = name;
    }
}

class Component : ComponentResource
{
    [Output("testOutput")]
    public Output<string> TestOutput { get; private set; } = default!;

    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
        // we are accessing input.Length to make sure this fails if input is null
        TestOutput = args.TestInput.Apply(input => $"{input}-{input.Length}");
    }
}
