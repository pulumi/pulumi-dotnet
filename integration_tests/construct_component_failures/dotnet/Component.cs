using Pulumi;

class Component : Pulumi.ComponentResource
{
    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts, remote: true)
    {
    }

    [Output("passwordResult")]
    public Output<string> PasswordResult { get; private set; } = default!;

    [Output("complexResult")]
    public Output<ComplexType> ComplexResult { get; set; } = default!;
}

class ComponentArgs : ResourceArgs
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
