using System.Threading.Tasks;
using Pulumi;

namespace TestProvider;

class ComponentArgs : ResourceArgs
{
    [Input("testInput")]
    public Input<string> TestInput { get; set; } = null!;
    [Input("testInputComplex")]
    public Input<ComplexTypeInput> TestInputComplex { get; set; } = null!;

}

[OutputType]
public sealed class ComplexTypeOutput
{
    [Output("name")]
    public string Name { get; set; }

    [Output("intValue")]
    public int IntValue { get; set; }

    [OutputConstructor]
    public ComplexTypeOutput(string name, int intValue)
    {
        Name = name;
        IntValue = intValue;
    }
}


public sealed class ComplexTypeInput: ResourceArgs
{
    [Input("name")]
    public string Name { get; set; } = default!;

    [Input("intValue")]
    public int IntValue { get; set; }
}


class Component : ComponentResource
{
    [Output("testOutput")]
    public Output<string> TestOutput { get; private set; } = default!;


    [Output("testOutputComplex")]
    public Output<ComplexTypeOutput> TestOutputComplex { get; set; } = null!;

    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
        TestOutput = args.TestInput.Apply(input => Output.Create(AsTask(input)));
        TestOutputComplex = args.TestInputComplex.Apply(a=> Output.Create(AsTask(new ComplexTypeOutput(a.Name,a.IntValue))));
    }

    private async Task<T> AsTask<T>(T value)
    {
        await Task.Delay(10);
        return value;
    }
}
