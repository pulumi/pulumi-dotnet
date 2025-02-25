using System;
using System.Text;
using System.Threading.Tasks;
using Pulumi;

public abstract class ComponentArgsBase : ResourceArgs {
    [Input("inheritInputAttribute")]
    public abstract Input<string> InheritInputAttribute  { get; protected set; }
}

public sealed class ComponentArgs : ComponentArgsBase
{
    [Input("passwordLength")]
    public Input<int> PasswordLength { get; protected set; } = null!;

    [Input("complex")]
    public Input<ComplexTypeArgs> Complex { get; protected set; } = null!;

    // the Input attribute is inherited from the base class
    public override Input<string> InheritInputAttribute  { get; protected set; } = null!;
}

public abstract class ComplexTypeArgsBase : ResourceArgs {
    [Input("inheritOutputAttribute")]
    public abstract string InheritInputAttribute  { get; set; }
}

public sealed class ComplexTypeArgs : ComplexTypeArgsBase
{
    [Input("name", required: true)]
    public string Name { get; set; } = null!;

    [Input("intValue", required: true)]
    public int IntValue { get; set; }

    // the Input attribute is inherited from the base class
    public override string InheritInputAttribute  { get; set; } = null!;
}

public abstract class ComplexTypeBase
{
    [Output("inheritOutputAttribute")]
    public abstract string InheritOutputAttribute { get; set; }
}

[OutputType]
public sealed class ComplexType : ComplexTypeBase
{
    [Output("name")]
    public string Name { get; set; }

    [Output("intValue")]
    public int IntValue { get; set; }

    // the Output attribute is inherited from the base class
    public override string InheritOutputAttribute { get; set; }

    [OutputConstructor]
    public ComplexType(string name, int intValue, string inheritOutputAttribute)
    {
        Name = name;
        IntValue = intValue;
        InheritOutputAttribute = inheritOutputAttribute;
    }
}

abstract class ComponentBase : ComponentResource
{
    protected ComponentBase(string type, string name, ResourceArgs? args, ComponentResourceOptions? options = null, bool remote = false)
        : base(type, name, args, options, remote)
    {
    }

    [Output("inheritOutputAttribute")]
    public abstract Output<string> InheritOutputAttribute { get; set; }
}

sealed class Component : ComponentBase
{
    private static readonly char[] Chars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    [Output("passwordResult")]
    public Output<string> PasswordResult { get; set; }

    [Output("complexResult")]
    public Output<ComplexType> ComplexResult { get; set; }

    // the Output attribute is inherited from the base class
    public override Output<string> InheritOutputAttribute { get; set; }

    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
        PasswordResult = args.PasswordLength.Apply(GenerateRandomString);
        ComplexResult = args.Complex.Apply(complex => Output.Create(AsTask(new ComplexType(complex.Name, complex.IntValue, complex.InheritInputAttribute))));
        InheritOutputAttribute = args.InheritInputAttribute;
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
