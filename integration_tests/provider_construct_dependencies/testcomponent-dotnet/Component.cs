using System.Threading.Tasks;
using Pulumi;

namespace TestProvider;

class ComponentArgs : ResourceArgs
{
    [Input("testInput")]
    public Input<string> TestInput { get; set; } = null!;
}


class Component : ComponentResource
{
    [Output("testOutput")]
    public Output<string> TestOutput { get; private set; } = default!;

    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
        TestOutput = args.TestInput.Apply(input => Output.Create(AsTask(input)));
    }

    private async Task<T> AsTask<T>(T value)
    {
        await Task.Delay(10);
        return value;
    }
}
