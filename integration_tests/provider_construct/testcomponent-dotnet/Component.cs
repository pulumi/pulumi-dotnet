using System;
using System.Text;
using System.Threading.Tasks;
using Pulumi;

public sealed class ComponentArgs : ResourceArgs
{
    [Input("passwordLength")]
    public Input<int> PasswordLength { get; set; } = null!;

    [Input("complex")]
    public Input<ComplexTypeArgs> Complex { get; set; } = null!;
}

public sealed class ComplexTypeArgs : global::Pulumi.ResourceArgs
{
    [Input("name", required: true)]
    public string Name { get; set; } = null!;

    [Input("intValue", required: true)]
    public int IntValue { get; set; }
}

[OutputType]
public sealed class ComplexType
{
    [Output("name")]
    public string Name { get; set; }

    [Output("intValue")]
    public int IntValue { get; set; }

    [OutputConstructor]
    public ComplexType(string name, int intValue)
    {
        Name = name;
        IntValue = intValue;
    }
}

class Component : ComponentResource
{
    private static readonly char[] Chars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    [Output("passwordResult")]
    public Output<string> PasswordResult { get; set; }

    [Output("complexResult")]
    public Output<ComplexType> ComplexResult { get; set; }

    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
        PasswordResult = args.PasswordLength.Apply(GenerateRandomString);
        ComplexResult = args.Complex.Apply(complex => Output.Create(AsTask(new ComplexType(complex.Name, complex.IntValue))));
    }

    private static Output<string> GenerateRandomString(int length)
    {
        var result = new StringBuilder(length);
        var random = new Random();

        for (var i = 0; i < length; i++)
        {
            result.Append(Chars[random.Next(Chars.Length)]);
        }

        return Output.CreateSecret(result.ToString());
    }

    private async Task<T> AsTask<T>(T value)
    {
        await Task.Delay(10);
        return value;
    }
}
